using System;

#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;
using WinUIWindow = Microsoft.UI.Xaml.Window;
#endif

namespace IhiksTaxi.UI;

// Системная плашка Windows убрана: шапку рисует сама страница. Здесь собраны
// операции над окном — свернуть, развернуть, закрыть, потянуть за шапку и
// перекрасить системную рамку под тему приложения.
public static class WindowChrome
{
	// ширина блока кнопок окна: три кнопки по 40 плюс отступы.
	// страница по ней понимает, где по шапке можно тянуть, а где кнопки
	public const double ButtonsWidth = 148;

#if WINDOWS
	private const uint WmClose = 0x0010;
	private const uint WmGetMinMaxInfo = 0x0024;
	private const uint MonitorDefaultToNearest = 2;

	// оформление рамки окна (Windows 11): без этих атрибутов система рисует
	// вокруг окна светлую полосу, которая в тёмной теме выдаёт себя полосой над шапкой
	private const int DwmwaUseImmersiveDarkMode = 20;
	private const int DwmwaUseImmersiveDarkModeLegacy = 19;
	private const int DwmwaCaptionColor = 35;
	private const int DwmwaBorderColor = 34;
	private const uint BorderDark = 0x00302414;   // #142430 в порядке BGR
	private const uint BorderLight = 0x00FFFFFF;

	[DllImport("user32.dll")]
	private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out NativePoint point);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

	[DllImport("comctl32.dll", SetLastError = true)]
	private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc proc, UIntPtr id, IntPtr data);

	[DllImport("comctl32.dll")]
	private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

	[DllImport("dwmapi.dll", PreserveSig = true)]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

	[DllImport("dwmapi.dll", PreserveSig = true)]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int size);

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

	private static AppWindow? _appWindow;
	private static IntPtr _subclassed;
	private static int _grabX;
	private static int _grabY;
	private static bool _dragging;
#endif

	private static VisualElement? _region;
	private static Action<bool>? _maximizedChanged;

	// Делает элемент шапкой окна: убирает системную плашку и следит за разворотом.
	public static void Attach(VisualElement header, Action<bool>? maximizedChanged = null)
	{
		Detach();
		_region = header;
		_maximizedChanged = maximizedChanged;
		header.HandlerChanged += OnHeaderChanged;
		header.SizeChanged += OnHeaderChanged;
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

	// Потянуть окно за шапку. Начало и продолжение — двумя вызовами, потому что
	// смещение считаем не по пальцу/мыши в координатах элементов (они ездят
	// вместе с окном), а по курсору на экране — так окно не «убегает» от мыши.
	public static void DragStart()
	{
#if WINDOWS
		_dragging = false;

		// развёрнутое окно тянем только после возврата к обычному размеру
		if (_appWindow is null || IsMaximized)
			return;

		if (!GetCursorPos(out var cursor))
			return;

		var position = _appWindow.Position;
		_grabX = cursor.X - position.X;
		_grabY = cursor.Y - position.Y;
		_dragging = true;
#endif
	}

	public static void DragMove()
	{
#if WINDOWS
		if (!_dragging || _appWindow is null || IsMaximized)
			return;

		if (!GetCursorPos(out var cursor))
			return;

		try
		{
			_appWindow.Move(new PointInt32(cursor.X - _grabX, cursor.Y - _grabY));
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось перетащить окно", ex);
		}
#endif
	}

	// Цвет рамки системы не подчиняется теме приложения: перекрашиваем её сами.
	public static void ApplyFrameTheme()
	{
#if WINDOWS
		var hwnd = MainHandle();
		if (hwnd == IntPtr.Zero)
			return;

		try
		{
			int dark = ThemeService.IsDark ? 1 : 0;
			if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
				DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref dark, sizeof(int));

			uint border = ThemeService.IsDark ? BorderDark : BorderLight;
			DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(uint));

			// шапку рисуем сами, но полосу рамки система красит цветом заголовка:
			// держим его в тон шапке, иначе над ней видна полоса чужого цвета
			int caption = ThemeService.IsDark ? (int)BorderDark : (int)BorderLight;
			DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось перекрасить рамку окна", ex);
		}
#endif
	}

	private static void Detach()
	{
		if (_region is not null)
		{
			_region.HandlerChanged -= OnHeaderChanged;
			_region.SizeChanged -= OnHeaderChanged;
			_region = null;
		}
	}

	private static void OnHeaderChanged(object? sender, EventArgs e) => TryApply();

	private static void TryApply()
	{
#if WINDOWS
		try
		{
			if (_region?.Handler?.PlatformView is not FrameworkElement)
				return;

			var native = NativeWindow();
			if (native is null)
				return;

			var hwnd = WindowNative.GetWindowHandle(native);
			var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
			var appWindow = AppWindow.GetFromWindowId(windowId);

			if (!ReferenceEquals(appWindow, _appWindow))
			{
				if (_appWindow is not null)
					_appWindow.Changed -= OnWindowChanged;

				_appWindow = appWindow;
				appWindow.Changed += OnWindowChanged;
			}

			// убираем системную шапку вместе с её кнопками; MAUI, увидев это,
			// перестаёт резервировать полосу под заголовок
			if (appWindow.Presenter is OverlappedPresenter presenter)
				presenter.SetBorderAndTitleBar(true, false);

			// контент занимает всё окно
			appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

			HookWindow(hwnd);
			ApplyFrameTheme();
			NotifyMaximized();
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось включить свою шапку окна", ex);
		}
#endif
	}

#if WINDOWS
	private static void HookWindow(IntPtr hwnd)
	{
		if (_subclassed == hwnd)
			return;

		try
		{
			_subclassed = hwnd;
			SetWindowSubclass(hwnd, _chromeProc, UIntPtr.Zero, IntPtr.Zero);
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось подписаться на сообщения окна", ex);
		}
	}

	private static IntPtr OnWindowMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr data)
	{
		try
		{
			// окно без системной шапки по умолчанию разворачивается на весь
			// монитор; здесь разворот укладываем в рабочую область
			if (message == WmGetMinMaxInfo && lParam != IntPtr.Zero)
				ClampMaximizedBounds(hwnd, lParam);
		}
		catch (Exception ex)
		{
			AppLog.Error("Ошибка обработки сообщения окна", ex);
		}

		return DefSubclassProc(hwnd, message, wParam, lParam);
	}

	private static void ClampMaximizedBounds(IntPtr hwnd, IntPtr lParam)
	{
		var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
		var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
		if (!GetMonitorInfo(monitor, ref info))
			return;

		var minMax = Marshal.PtrToStructure<MinMaxInfo>(lParam);
		minMax.MaxPosition.X = info.Work.Left - info.Monitor.Left;
		minMax.MaxPosition.Y = info.Work.Top - info.Monitor.Top;
		minMax.MaxSize.X = info.Work.Right - info.Work.Left;
		minMax.MaxSize.Y = info.Work.Bottom - info.Work.Top;
		Marshal.StructureToPtr(minMax, lParam, false);
	}

	private static void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
	{
		if (args.DidPresenterChange)
			NotifyMaximized();
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
