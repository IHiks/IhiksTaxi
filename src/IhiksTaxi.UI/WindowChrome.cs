using System;

#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;
using WinUIWindow = Microsoft.UI.Xaml.Window;
#endif

namespace IhiksTaxi.UI;

// Системная плашка Windows убрана: шапку рисует сама страница, а здесь
// собраны операции над окном — область перетаскивания и кнопки окна.
public static class WindowChrome
{
#if WINDOWS
	private const uint WmClose = 0x0010;

	// ширина блока кнопок окна: три кнопки по 40 плюс отступы
	private const double ButtonsWidth = 148;

	[DllImport("user32.dll")]
	private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	private static AppWindow? _appWindow;
	private static FrameworkElement? _regionElement;
	private static RectInt32 _lastRegion;
	private static bool _hasLastRegion;
	private static bool _hooked;
	private static bool _subclassed;

	private const uint WmGetMinMaxInfo = 0x0024;
	private const uint MonitorDefaultToNearest = 2;

	[StructLayout(LayoutKind.Sequential)]
	private struct NativePoint
	{
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MinMaxInfo
	{
		public NativePoint Reserved;
		public NativePoint MaxSize;
		public NativePoint MaxPosition;
		public NativePoint MinTrackSize;
		public NativePoint MaxTrackSize;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct NativeRect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct MonitorInfo
	{
		public int Size;
		public NativeRect Monitor;
		public NativeRect Work;
		public uint Flags;
	}

	private delegate IntPtr SubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr data);

	private static readonly SubclassProc _chromeProc = OnWindowMessage;

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

	[DllImport("comctl32.dll", SetLastError = true)]
	private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc proc, UIntPtr id, IntPtr data);

	[DllImport("comctl32.dll")]
	private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
#endif

	private static VisualElement? _region;
	private static Action<bool>? _maximizedChanged;

	// Делает элемент областью перетаскивания и убирает системную шапку окна.
	public static void Attach(VisualElement dragRegion, Action<bool>? maximizedChanged = null)
	{
		Detach();
		_region = dragRegion;
		_maximizedChanged = maximizedChanged;
		dragRegion.HandlerChanged += OnRegionChanged;
		dragRegion.SizeChanged += OnRegionChanged;
		TryApply();
	}

	public static void Minimize()
	{
#if WINDOWS
		if (Presenter() is { } presenter)
			presenter.Minimize();
#endif
	}

	public static void ToggleMaximize()
	{
#if WINDOWS
		if (Presenter() is not { } presenter)
			return;

		if (presenter.State == OverlappedPresenterState.Maximized)
			presenter.Restore();
		else
			presenter.Maximize();

		NotifyMaximized();
#endif
	}

	public static void Close()
	{
#if WINDOWS
		var hwnd = MainHandle();
		if (hwnd != IntPtr.Zero)
			PostMessageW(hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
		else
			Microsoft.Maui.Controls.Application.Current?.Quit();
#endif
	}

	private static void Detach()
	{
		if (_region is not null)
		{
			_region.HandlerChanged -= OnRegionChanged;
			_region.SizeChanged -= OnRegionChanged;
			_region = null;
		}
	}

	private static void OnRegionChanged(object? sender, EventArgs e) => TryApply();

	private static void TryApply()
	{
#if WINDOWS
		try
		{
			if (_region?.Handler?.PlatformView is not FrameworkElement element)
				return;

			var native = NativeWindow();
			if (native is null)
				return;

			var hwnd = WindowNative.GetWindowHandle(native);
			var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
			var appWindow = AppWindow.GetFromWindowId(windowId);

			if (!ReferenceEquals(appWindow, _appWindow))
			{
				if (_appWindow is not null && _hooked)
					_appWindow.Changed -= OnWindowChanged;

				_appWindow = appWindow;
				appWindow.Changed += OnWindowChanged;
				_hooked = true;
				_hasLastRegion = false;
			}

			// убираем системную шапку вместе с её кнопками; MAUI, увидев это,
			// перестаёт резервировать полосу под заголовок
			if (appWindow.Presenter is OverlappedPresenter presenter)
				presenter.SetBorderAndTitleBar(true, false);

			// контент занимает всё окно
			appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

			HookMaximizeBounds(hwnd);

			_regionElement = element;
			UpdateDragRegion();
			NotifyMaximized();
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось включить свою шапку окна", ex);
		}
#endif
	}

#if WINDOWS
	private static void HookMaximizeBounds(IntPtr hwnd)
	{
		if (_subclassed)
			return;

		try
		{
			_subclassed = SetWindowSubclass(hwnd, _chromeProc, UIntPtr.Zero, IntPtr.Zero);
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось ограничить разворот окна рабочей областью", ex);
		}
	}

	// окно без системной шапки по умолчанию разворачивается на весь монитор;
	// здесь разворот укладывается в рабочую область, чтобы панель задач осталась видна
	private static IntPtr OnWindowMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr data)
	{
		var result = DefSubclassProc(hwnd, message, wParam, lParam);

		if (message != WmGetMinMaxInfo || lParam == IntPtr.Zero)
			return result;

		try
		{
			var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
			var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
			if (!GetMonitorInfo(monitor, ref info))
				return result;

			var minMax = Marshal.PtrToStructure<MinMaxInfo>(lParam);
			minMax.MaxPosition.X = info.Work.Left - info.Monitor.Left;
			minMax.MaxPosition.Y = info.Work.Top - info.Monitor.Top;
			minMax.MaxSize.X = info.Work.Right - info.Work.Left;
			minMax.MaxSize.Y = info.Work.Bottom - info.Work.Top;
			Marshal.StructureToPtr(minMax, lParam, false);
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось развернуть окно по размеру рабочей области", ex);
		}

		return result;
	}

	private static void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
	{
		if (args.DidSizeChange)
			UpdateDragRegion();

		if (args.DidPresenterChange)
			NotifyMaximized();
	}

	private static void UpdateDragRegion()
	{
		try
		{
			if (_appWindow is null || _regionElement is null)
				return;

			double scale = _regionElement.XamlRoot?.RasterizationScale ?? 1.0;
			var bounds = _regionElement.TransformToVisual(null)
				.TransformBounds(new Windows.Foundation.Rect(0, 0, _regionElement.ActualWidth, _regionElement.ActualHeight));

			int left = (int)Math.Round(bounds.X * scale);
			int top = (int)Math.Round(bounds.Y * scale);
			int width = (int)Math.Round((bounds.Width - ButtonsWidth) * scale);
			int height = (int)Math.Round(bounds.Height * scale);
			if (width <= 0 || height <= 0)
				return;

			var rect = new RectInt32(left, top, width, height);
			if (_hasLastRegion && rect.X == _lastRegion.X && rect.Y == _lastRegion.Y
				&& rect.Width == _lastRegion.Width && rect.Height == _lastRegion.Height)
				return;

			_lastRegion = rect;
			_hasLastRegion = true;

			var source = InputNonClientPointerSource.GetForWindowId(_appWindow.Id);

			source.SetRegionRects(NonClientRegionKind.Caption, new[] { rect });

			// области системных кнопок окна перехватывают клики в правом верхнем
			// углу даже когда шапки нет — очищаем их, иначе своя кнопка закрытия мертва
			source.SetRegionRects(NonClientRegionKind.Close, Array.Empty<RectInt32>());
			source.SetRegionRects(NonClientRegionKind.Minimize, Array.Empty<RectInt32>());
			source.SetRegionRects(NonClientRegionKind.Maximize, Array.Empty<RectInt32>());
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось задать область перетаскивания окна", ex);
		}
	}

	private static void NotifyMaximized() => _maximizedChanged?.Invoke(IsMaximized);

	private static bool IsMaximized => Presenter()?.State == OverlappedPresenterState.Maximized;

	private static OverlappedPresenter? Presenter() => _appWindow?.Presenter as OverlappedPresenter;

	private static IntPtr MainHandle()
	{
		var native = NativeWindow();
		return native is null ? IntPtr.Zero : WindowNative.GetWindowHandle(native);
	}

	private static WinUIWindow? NativeWindow()
	{
		var windows = Microsoft.Maui.Controls.Application.Current?.Windows;
		if (windows is null || windows.Count == 0)
			return null;

		return windows[0].Handler?.PlatformView as WinUIWindow;
	}
#endif
}
