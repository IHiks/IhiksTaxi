using System;
using System.Collections.Generic;
using MauiApplication = Microsoft.Maui.Controls.Application;

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
// операции над окном — свернуть, развернуть, закрыть, потянуть за шапку,
// перекрасить системную рамку под тему и подсветить кнопки окна.
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

	// стиль окна: у обычного окна есть утолщённая рамка для изменения размера.
	// Она же остаётся и в развёрнутом окне — 7 пикселей по краям, из-за которых
	// содержимое не доходит до края экрана. На весь экран рамку убираем.
	private const int GwlStyle = -16;
	private const int WsCaption = 0x00C00000;
	private const int WsThickFrame = 0x00040000;
	private const int WsPopup = unchecked((int)0x80000000);
	private const uint SwpNoZOrder = 0x0004;
	private const uint SwpNoMove = 0x0002;
	private const uint SwpNoSize = 0x0001;
	private const uint SwpFrameChanged = 0x0020;

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr hWnd, int index);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int width, int height, uint flags);

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

	// место и размер окна до разворота на весь экран
	private static PointInt32 _restorePosition;
	private static SizeInt32 _restoreBounds = new(960, 720);
	private static bool _fullScreen;
	private static bool _switching;

	private static RectInt32 RestoreBounds()
		=> new(_restorePosition.X, _restorePosition.Y, _restoreBounds.Width, _restoreBounds.Height);
#endif
	private static VisualElement? _region;
	private static Action<bool>? _maximizedChanged;

	private enum ButtonState
	{
		Normal,
		Hover,
		Pressed,
	}

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

	// Разворот на весь экран. Обычный Maximize оставляет вокруг окна полосу
	// системной рамки (7 px) — она и выглядит как пустота по краям. Поэтому
	// на весь экран убираем рамку совсем: клиентская область занимает весь
	// монитор, от края до края, и приложение обволакивает экран.
	public static void ToggleMaximize()
	{
#if WINDOWS
		if (_appWindow is null || _switching)
			return;

		var fullScreen = !_fullScreen;
		_fullScreen = fullScreen;
		NotifyMaximized();

		_switching = true;
		try
		{
			if (fullScreen)
			{
				_restorePosition = _appWindow.Position;
				_restoreBounds = _appWindow.Size;

				// сначала убираем рамку, потом растягиваем окно: в обратном
				// порядке система перестраивает окно посреди перемещения
				SetFrameVisible(false);
				SetResizableFrame(false);
				_appWindow.MoveAndResize(MonitorBounds());
			}
			else
			{
				_appWindow.MoveAndResize(RestoreBounds());
				SetResizableFrame(true);
				SetFrameVisible(true);
			}

			// окно могло свернуться, пока перестраивалось: поднимаем его обратно
			RestoreIfMinimized(fullScreen);
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось развернуть окно на весь экран", ex);
		}
		finally
		{
			_switching = false;
		}
#endif
	}

#if WINDOWS
	// Окно в момент перестройки рамки иногда уходит в свёрнутое состояние.
	// Возвращаем его на место, чтобы щелчок по кнопке всегда давал результат.
	private static void RestoreIfMinimized(bool fullScreen)
	{
		var presenter = Presenter();
		if (presenter?.State != OverlappedPresenterState.Minimized)
			return;

		presenter.Restore();
		_appWindow?.MoveAndResize(fullScreen ? MonitorBounds() : RestoreBounds());
	}

	// размер рабочей области монитора в пикселях: WinUI считает в dip, окно —
	// в пикселях. Рабочая область, а не весь монитор: панель задач должна
	// оставаться видна, иначе закрыть/свернуть приложение негде
	private static RectInt32 MonitorBounds()
	{
		var display = DisplayArea.GetFromWindowId(_appWindow!.Id, DisplayAreaFallback.Primary);
		var area = display.WorkArea;
		double scale = (_region?.Handler?.PlatformView as FrameworkElement)?.XamlRoot?.RasterizationScale ?? 1.0;

		return new RectInt32(
			(int)Math.Round(area.X * scale), (int)Math.Round(area.Y * scale),
			(int)Math.Round(area.Width * scale), (int)Math.Round(area.Height * scale));
	}

	// Рамку системы включаем и выключаем повторно: окно без рамки и без
	// заголовка, но с клиентской областью на весь монитор даёт больше места
	// на экране и не показывает полос вокруг содержимого.
	private static void SetFrameVisible(bool visible)
	{
		if (_appWindow?.Presenter is not OverlappedPresenter presenter)
			return;

		presenter.SetBorderAndTitleBar(visible, false);
	}

	// Меняет стиль окна: убирает или возвращает рамку изменения размера.
	// WinUI оставляет её и в развёрнутом окне — 7 пикселей по краям, из-за
	// которых содержимое не доходит до краёв монитора. Стиль меняем только
	// до или после перемещения окна: перестройка окна посреди MoveAndResize
	// бросает его в свёрнутое состояние.
	private static void SetResizableFrame(bool visible)
	{
		var hwnd = MainHandle();
		if (hwnd == IntPtr.Zero)
			return;

		try
		{
			int style = GetWindowLong(hwnd, GwlStyle);
			// Caption не возвращаем: заголовок у нашего окна всегда свой,
			// системный появлялся после возврата из полного экрана
			int wanted = visible
				? (style | WsThickFrame) & ~(WsCaption | WsPopup)
				: style & ~(WsCaption | WsThickFrame);

			if (wanted == style)
				return;

			SetWindowLong(hwnd, GwlStyle, wanted);
			SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
				SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось переключить рамку окна", ex);
		}
	}
#endif

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

		if (_appWindow is null || !GetCursorPos(out var cursor))
			return;

		if (_fullScreen)
		{
			// с полноэкранного окна шапку тянем вниз — вернём обычный размер
			_dragging = true;
			_grabX = cursor.X - _appWindow.Position.X;
			_grabY = cursor.Y - _appWindow.Position.Y;
			return;
		}

		var position = _appWindow.Position;
		_grabX = cursor.X - position.X;
		_grabY = cursor.Y - position.Y;
		_dragging = true;
#endif
	}

	// кромка экрана, у которой окно открывается на весь экран
	private const int SnapEdge = 6;

	// на сколько пикселей ниже кромки хватаем шапку, возвращая окно из полного экрана
	private const int SnapGrab = 12;

	public static void DragMove()
	{
#if WINDOWS
		if (!_dragging || _appWindow is null)
			return;

		if (!GetCursorPos(out var cursor))
			return;

		try
		{
			var area = MonitorBounds();

			if (_fullScreen)
			{
				// у верхней кромки остаёмся на весь экран
				if (cursor.Y <= area.Y + SnapEdge)
					return;

				// потянули вниз — возвращаем обычный размер, и окно сразу
				// оказывается под курсором, а не прыгает на старое место
				ToggleMaximize();
				var bounds = RestoreBounds();
				int x = cursor.X - bounds.Width / 2;
				if (area.Width > bounds.Width)
					x = Math.Clamp(x, area.X, area.X + area.Width - bounds.Width);
				else
					x = area.X;
				int y = Math.Max(area.Y, cursor.Y - SnapGrab);
				_appWindow.Move(new PointInt32(x, y));
				_grabX = cursor.X - x;
				_grabY = cursor.Y - y;
				return;
			}

			// потянули к верхней кромке — открываем на весь экран
			if (cursor.Y <= area.Y + SnapEdge)
			{
				_dragging = false;
				ToggleMaximize();
				return;
			}

			_appWindow.Move(new PointInt32(cursor.X - _grabX, cursor.Y - _grabY));
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось перетащить окно", ex);
		}
#endif
	}

	// Двойной щелчок по шапке разворачивает окно, но по кнопкам окна — нет.
	public static bool IsButtonsArea(VisualElement header, double x)
		=> x >= header.Width - ButtonsWidth;

	// Цвет рамки системы не подчиняется теме приложения: перекрашиваем её сами.
	// Рамка видна по краям окна, поэтому берём цвет страницы, а верхнюю полосу
	// (её система красит цветом заголовка) — в тон шапке. Иначе по краям
	// остаются полосы чужого цвета, похожие на пустоту.
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

			uint border = ToColorRef(ThemeColor("PageBackground"));
			DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(uint));

			uint caption = ToColorRef(ThemeColor("HeaderBackground"));
			DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(uint));
		}
		catch (Exception ex)
		{
			AppLog.Error("Не удалось перекрасить рамку окна", ex);
		}
#endif
	}

	// Windows ждёт цвет в порядке BGR, а не RGB
	private static uint ToColorRef(Color color)
		=> (uint)(((int)Math.Round(color.Blue * 255) << 16)
			| ((int)Math.Round(color.Green * 255) << 8)
			| (int)Math.Round(color.Red * 255));

	// Кнопки шапки ведут себя как системные: наведение, нажатие и возврат красим
	// здесь, по событиям указателя. В стиле это не работает: MAUI оставлял на
	// кнопке цвет нажатия навсегда, и кнопка становилась оранжевой. Цвета берём
	// из палитры в момент события, поэтому смена темы тоже ничего не оставляет
	// «залипшим».
	public static void TieWindowButtons(VisualElement owner, Button minimize, Button maximize, Button close)
	{
		UntieWindowButtons(owner);

		TieButton(owner, minimize, false);
		TieButton(owner, maximize, false);
		TieButton(owner, close, true);

		EnsureThemeHook();
		HookWindowState();
	}

	private static void TieButton(VisualElement owner, Button button, bool isClose)
	{
		// у кнопки нет своих событий указателя: их даёт распознаватель жестов
		var pointer = new PointerGestureRecognizer();
		var state = new WindowButtonState(owner, button, pointer, isClose);

		state.PointerEntered = (_, _) => Enter(state);
		state.PointerExited = (_, _) => Exit(state);
		state.PointerPressed = (_, _) => Press(state, true);
		state.PointerReleased = (_, _) => Press(state, false);
		state.Clicked = (_, _) => Press(state, false);

		pointer.PointerEntered += state.PointerEntered;
		pointer.PointerExited += state.PointerExited;
		pointer.PointerPressed += state.PointerPressed;
		pointer.PointerReleased += state.PointerReleased;
		button.Clicked += state.Clicked;

		button.GestureRecognizers.Add(pointer);
		_buttons.Add(state);
	}

	public static void UntieWindowButtons(VisualElement owner)
	{
		foreach (var state in _buttons.FindAll(item => ReferenceEquals(item.Owner, owner)))
		{
			// обработчики снимаем явно: иначе закрытые страницы остаются
			// жить в своих кнопках и щёлкают цветами невидимых кнопок
			state.Pointer.PointerEntered -= state.PointerEntered;
			state.Pointer.PointerExited -= state.PointerExited;
			state.Pointer.PointerPressed -= state.PointerPressed;
			state.Pointer.PointerReleased -= state.PointerReleased;
			state.Button.Clicked -= state.Clicked;

			state.Button.GestureRecognizers.Remove(state.Pointer);
			_buttons.Remove(state);
		}
	}

	// состояние одной кнопки окна
	private sealed class WindowButtonState
	{
		public WindowButtonState(VisualElement owner, Button button, PointerGestureRecognizer pointer, bool isClose)
		{
			Owner = owner;
			Button = button;
			Pointer = pointer;
			IsClose = isClose;
		}

		public VisualElement Owner { get; }
		public Button Button { get; }
		public PointerGestureRecognizer Pointer { get; }
		public bool IsClose { get; }
		public bool Hovered { get; set; }
		public bool Pressed { get; set; }

		// обработчики храним, чтобы их можно было отцепить
		public EventHandler<PointerEventArgs> PointerEntered { get; set; } = null!;
		public EventHandler<PointerEventArgs> PointerExited { get; set; } = null!;
		public EventHandler<PointerEventArgs> PointerPressed { get; set; } = null!;
		public EventHandler<PointerEventArgs> PointerReleased { get; set; } = null!;
		public EventHandler Clicked { get; set; } = null!;
	}

	private static readonly List<WindowButtonState> _buttons = new();

	private static void Enter(WindowButtonState state)
	{
		state.Hovered = true;
		Paint(state);
	}

	private static void Exit(WindowButtonState state)
	{
		state.Hovered = false;
		state.Pressed = false;
		Paint(state);
	}

	private static void Press(WindowButtonState state, bool pressed)
	{
		state.Pressed = pressed;
		Paint(state);
	}

	// смена темы во время наведения не должна оставить цвет прошлой темы
	private static bool _themeHooked;

	private static void EnsureThemeHook()
	{
		if (_themeHooked)
			return;

		ThemeService.Changed += RepaintButtons;
		_themeHooked = true;
	}

	private static void RepaintButtons()
	{
		AppLog.Info($"Шапка: перекраска при смене темы (тёмная={ThemeService.IsDark}, кнопок={_buttons.Count})");
		foreach (var state in _buttons)
			Paint(state);
	}

	// Окно свернули или увели с экрана — указатель ушёл вместе с ним, но
	// сообщения об уходе мыши не будет. Сбрасываем подсветку сами.
	private static Microsoft.Maui.Controls.Window? _hookedWindow;

	private static void HookWindowState()
	{
		var windows = MauiApplication.Current?.Windows;
		if (windows is null || windows.Count == 0)
			return;

		var window = windows[0];
		if (ReferenceEquals(window, _hookedWindow))
			return;

		if (_hookedWindow is not null)
		{
			_hookedWindow.Deactivated -= OnWindowStateChanged;
			_hookedWindow.Activated -= OnWindowStateChanged;
		}

		_hookedWindow = window;
		window.Deactivated += OnWindowStateChanged;
		window.Activated += OnWindowStateChanged;
	}

	private static void OnWindowStateChanged(object? sender, EventArgs e)
	{
		foreach (var state in _buttons)
		{
			state.Hovered = false;
			state.Pressed = false;
			Paint(state);
		}
	}

	// Красит кнопку окна под текущее состояние указателя.
	private static void Paint(WindowButtonState state)
	{
		var button = state.Button;
		var value = !state.Hovered
			? ButtonState.Normal
			: state.Pressed ? ButtonState.Pressed : ButtonState.Hover;

		if (state.IsClose)
		{
			button.BackgroundColor = value switch
			{
				ButtonState.Hover => ThemeColor("WindowCloseHover"),
				ButtonState.Pressed => ThemeColor("WindowClosePressed"),
				_ => ThemeColor("HeaderBackground"),
			};

			button.TextColor = value == ButtonState.Normal
				? ThemeColor("MutedText")
				: ThemeColor("WindowCloseForeground");
			return;
		}

		button.BackgroundColor = value switch
		{
			ButtonState.Hover => ThemeColor("WindowButtonHover"),
			ButtonState.Pressed => ThemeColor("WindowButtonPressed"),
			_ => ThemeColor("HeaderBackground"),
		};

		button.TextColor = value == ButtonState.Normal
			? ThemeColor("MutedText")
			: ThemeColor("WindowButtonHoverForeground");
	}

	// цвет из палитры по текущей теме
	private static Color ThemeColor(string key)
	{
		var name = ThemeService.IsDark ? key + "Dark" : key;
		if (MauiApplication.Current?.Resources.TryGetValue(name, out var value) == true && value is Color color)
			return color;

		AppLog.Info($"Шапка: в палитре нет цвета {name}");
		return Colors.Transparent;
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
		// Режим «на весь экран» заканчивается, только когда окно свернули.
		// Изменения размеров и смену рамки игнорируем: их же шлёт наш
		// собственный разворот, и режим сбрасывался сразу после включения.
		if (!_fullScreen || Presenter()?.State != OverlappedPresenterState.Minimized)
			return;

		_fullScreen = false;
		NotifyMaximized();
	}

	private static void NotifyMaximized() => _maximizedChanged?.Invoke(IsMaximized);

	private static bool IsMaximized => _fullScreen;

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
