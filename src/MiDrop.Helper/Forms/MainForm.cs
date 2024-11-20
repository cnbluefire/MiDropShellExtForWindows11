using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32.Foundation;
using System.Runtime.CompilerServices;
using Windows.Win32.Graphics.Gdi;
using System.Runtime.InteropServices;
using Windows.UI.Composition.Desktop;
using Windows.UI.Composition;
using Microsoft.Win32;
using MiDrop.Helper.Utils;

using WINDOW_STYLE = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE;
using WINDOW_EX_STYLE = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE;

namespace MiDrop.Helper.Forms
{
    public partial class MainForm : Form
    {
        private const int WindowWidth = 120;
        private const int WindowHeight = 12;

        private const float GeometryMaxWidth = WindowWidth - 10;
        private const float GeometryMaxHeight = 40;
        private const float GeometryWidth = GeometryMaxWidth;
        private const float GeometryHiddenHeight = 0;
        private const float GeometryTipHeight = WindowHeight - 8;
        private const float GeometryDragOverHeight = WindowHeight - 2;

        private DesktopWindowTarget? desktopWindowTarget;
        private SpriteVisual? rootVisual;
        private ShapeVisual? shapeVisual;

        private NotifyIcon notifyIcon;

        public MainForm()
        {
            this.AllowDrop = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.ClientSize = default;

            InitializeNotifyIcon();

            var weakThis = new WeakReference(this);
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            DragDropListener.DragDropStarted += OnSystemDragDropStarted;
            DragDropListener.DragDropStopped += OnSystemDragDropStopped;

            void OnDisplaySettingsChanged(object? sender, EventArgs e)
            {
                if (weakThis.Target is MainForm strongThis)
                {
                    SetBounds(WindowWidth, WindowHeight);
                    UpdateRootVisualSize();
                    UpdateNotifyIcon();
                }
                else
                {
                    SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                }
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;

                unchecked
                {
                    cp.Style &= ~(int)WINDOW_STYLE.WS_OVERLAPPEDWINDOW;
                    cp.Style |= (int)WINDOW_STYLE.WS_POPUP;

                    cp.ExStyle |= (int)WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP;
                    cp.ExStyle |= (int)WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
                }

                return cp;
            }
        }

        #region DragDrop

        protected override void OnDragOver(DragEventArgs drgevent)
        {
            base.OnDragOver(drgevent);
            drgevent.Effect = DragDropEffects.Copy;

            if (shapeVisual != null)
            {
                shapeVisual.Offset = GetShapeVisualOffset(ShapeVisualState.DragOver);
            }
        }

        protected override void OnDragLeave(EventArgs e)
        {
            base.OnDragLeave(e);

            if (shapeVisual != null)
            {
                if (DragDropListener.IsDragging)
                {
                    shapeVisual.Offset = GetShapeVisualOffset(ShapeVisualState.Tip);
                }
                else
                {
                    shapeVisual.Offset = GetShapeVisualOffset(ShapeVisualState.Hidden);
                }
            }

            SetBounds(WindowWidth, WindowHeight);
            UpdateRootVisualSize();
        }

        protected override async void OnDragDrop(DragEventArgs drgevent)
        {
            base.OnDragDrop(drgevent);

            if (shapeVisual != null)
            {
                shapeVisual.Offset = GetShapeVisualOffset(ShapeVisualState.Hidden);
            }

            var data = drgevent.Data;
            if (data != null)
            {
                var dataValues = await DataObjectHelper.ProcessDataObjectAsync(data, default);
                if (dataValues != null && dataValues.Length > 0)
                {
                    var files = dataValues
                        .Where(c => c.DataType == DataObjectHelper.DataType.FilePath && !string.IsNullOrEmpty(c.Value))
                        .Select(c => c.Value!)
                        .ToArray();

                    if (files.Length > 0)
                    {
                        await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync(default);
                        await MiDrop.Core.XiaomiPcManagerHelper.SendFilesAsync(files, TimeSpan.FromSeconds(5));
                    }

                    var text = dataValues
                        .FirstOrDefault(c => c.DataType == DataObjectHelper.DataType.Text && !string.IsNullOrEmpty(c.Value))?
                        .Value;

                    if (!string.IsNullOrEmpty(text))
                    {
                        await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync(default);
                        for (int i = 0; i < 3; i++)
                        {
                            try
                            {
                                Clipboard.SetData(DataFormats.Text, text);
                                break;
                            }
                            catch { }
                        }
                    }
                }

                return;
            }
        }


        private void OnSystemDragDropStarted(object? sender, EventArgs e)
        {
            if (shapeVisual != null)
            {
                shapeVisual.Offset = GetShapeVisualOffset(ShapeVisualState.Tip);
            }
        }

        private void OnSystemDragDropStopped(object? sender, EventArgs e)
        {
            if (shapeVisual != null)
            {
                shapeVisual.Offset = GetShapeVisualOffset(ShapeVisualState.Hidden);
            }
        }


        #endregion DragDrop

        #region Window Lifetime

        protected override void OnHandleCreated(EventArgs e)
        {
            if (desktopWindowTarget != null)
            {
                desktopWindowTarget?.Dispose();
                desktopWindowTarget = null;
            }

            base.OnHandleCreated(e);

            if (Environment.OSVersion.Version >= new Version(10, 0, 19041, 0))
            {
                Windows.Win32.PInvoke.SetWindowDisplayAffinity((HWND)Handle, Windows.Win32.UI.WindowsAndMessaging.WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE);
            }

            desktopWindowTarget = CompositionHelper.CreateDesktopWindowTarget(Handle, true);
            if (desktopWindowTarget != null)
            {
                rootVisual = CompositionHelper.Compositor.CreateSpriteVisual();
                rootVisual.RelativeSizeAdjustment = new System.Numerics.Vector2(1, 1);
                rootVisual.Clip = CompositionHelper.Compositor.CreateInsetClip();

                desktopWindowTarget.Root = rootVisual;

                var geometry = CompositionHelper.Compositor.CreateRoundedRectangleGeometry();
                geometry.Size = new System.Numerics.Vector2(GeometryWidth, GeometryMaxHeight);
                geometry.CornerRadius = new System.Numerics.Vector2(4);

                var shape = CompositionHelper.Compositor.CreateSpriteShape(geometry);
                shape.FillBrush = CompositionHelper.Compositor.CreateColorBrush(Windows.UI.Color.FromArgb(220, 255, 255, 255));
                shape.StrokeBrush = CompositionHelper.Compositor.CreateColorBrush(Windows.UI.Color.FromArgb(80, 0, 0, 0));
                shape.StrokeThickness = 1f;

                var shapeVisualOffset = GetShapeVisualOffset(ShapeVisualState.Hidden);

                shapeVisual = CompositionHelper.Compositor.CreateShapeVisual();
                shapeVisual.Size = new System.Numerics.Vector2(GeometryWidth, GeometryMaxHeight);
                shapeVisual.Offset = shapeVisualOffset;
                shapeVisual.Shapes.Add(shape);

                var offsetAnimation = CompositionHelper.Compositor.CreateVector3KeyFrameAnimation();
                offsetAnimation.InsertExpressionKeyFrame(1f, "this.FinalValue");
                offsetAnimation.Duration = TimeSpan.FromSeconds(0.3);
                offsetAnimation.Target = "Offset";

                var imp = CompositionHelper.Compositor.CreateImplicitAnimationCollection();
                imp[offsetAnimation.Target] = offsetAnimation;
                shapeVisual.ImplicitAnimations = imp;

                rootVisual.Children.InsertAtTop(shapeVisual);

                SetBounds(WindowWidth, WindowHeight);
                UpdateRootVisualSize();
            }
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);

            SetBounds(WindowWidth, WindowHeight);
            UpdateRootVisualSize();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            DragDropListener.DragDropStarted -= OnSystemDragDropStarted;
            DragDropListener.DragDropStopped -= OnSystemDragDropStopped;

            desktopWindowTarget?.Dispose();
            desktopWindowTarget = null;
        }

        #endregion Window Lifetime

        #region Notify Icon

        [MemberNotNull(nameof(notifyIcon))]
        private void InitializeNotifyIcon()
        {
            const string MenuItemName_OpenXiaomiPcManager = "open-xiaomi-pc-manager";
            //const string MenuItemName_Settings = "settings";
            const string MenuItemName_Exit = "exit";

            notifyIcon = new NotifyIcon()
            {
                Text = "MiDrop Helper",
                ContextMenuStrip = new ContextMenuStrip()
                {
                    Items =
                    {
                        new ToolStripMenuItem("MiDrop Helper")
                        {
                            Enabled = false
                        },
                        new ToolStripMenuItem("打开小米电脑管家",null, OnNotifyIconMenuItemClick, MenuItemName_OpenXiaomiPcManager),
                        //new ToolStripMenuItem("MiDrop Helper 设置",null, OnNotifyIconMenuItemClick, MenuItemName_Settings),
                        new ToolStripMenuItem("退出",null, OnNotifyIconMenuItemClick, MenuItemName_Exit)
                    },
                    RenderMode = ToolStripRenderMode.System,
                },
            };
            notifyIcon.DoubleClick += OnNotifyIconDoubleClick;
            UpdateNotifyIcon();
            notifyIcon.Visible = true;

            async void OnNotifyIconDoubleClick(object? sender, EventArgs e)
            {
                await OpenXiaomiPcManagerAsync();
            }

            async void OnNotifyIconMenuItemClick(object? sender, EventArgs e)
            {
                if (sender is ToolStripMenuItem menuItem)
                {
                    switch (menuItem.Name)
                    {
                        case MenuItemName_OpenXiaomiPcManager:
                            {
                                await OpenXiaomiPcManagerAsync();
                            }
                            break;

                        case MenuItemName_Exit:
                            {
                                if (notifyIcon != null) notifyIcon.Visible = false;
                                Application.Exit();
                            }
                            break;
                    }
                }
            }

            async Task OpenXiaomiPcManagerAsync()
            {
                try
                {
                    await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync("--open_controlcenter", default);
                }
                catch { }
            }
        }

        private void UpdateNotifyIcon()
        {
            if (notifyIcon == null) return;

            var executeFile = MiDrop.Core.XiaomiPcManagerHelper.GetXiaomiPcManagerExecuteFile();
            if (string.IsNullOrEmpty(executeFile)) return;

            try
            {
                var oldIcon = notifyIcon.Icon;
                notifyIcon.Icon = Icon.ExtractIcon(executeFile, -32512, 32);
                oldIcon?.Dispose();
            }
            catch { }
        }

        #endregion Notify Icon

        private static System.Numerics.Vector3 GetShapeVisualOffset(ShapeVisualState shapeVisualState)
        {
            var height = shapeVisualState switch
            {
                ShapeVisualState.Tip => GeometryTipHeight,
                ShapeVisualState.DragOver => GeometryDragOverHeight,
                _ => GeometryHiddenHeight,
            };
            return new System.Numerics.Vector3((WindowWidth - GeometryWidth) / 2, -GeometryMaxHeight + height, 0);
        }

        private enum ShapeVisualState
        {
            Hidden,
            Tip,
            DragOver
        }

        private void UpdateRootVisualSize()
        {
            if (rootVisual != null)
            {
                var dpi = Windows.Win32.PInvoke.GetDpiForWindow((HWND)Handle);
                rootVisual.Scale = new System.Numerics.Vector3(dpi / 96f, dpi / 96f, 1);
                rootVisual.Size = new System.Numerics.Vector2(80, 10);
            }
        }

        public void SetBounds(int width, int height)
        {
            var screen = Screen.PrimaryScreen;
            if (screen == null) return;

            var workingArea = screen.WorkingArea;

            var monitor = GetMonitorForScreen(screen);
            if (Windows.Win32.PInvoke.GetDpiForMonitor((HMONITOR)monitor, Windows.Win32.UI.HiDpi.MONITOR_DPI_TYPE.MDT_DEFAULT, out var dpiX, out var dpiY).Failed)
            {
                dpiX = 96;
                dpiY = 96;
            }

            var contentSize = new Size((int)(width * dpiY / 96), (int)(height * dpiY / 96));

            Windows.Win32.PInvoke.SetWindowPos(
                (HWND)Handle,
                (HWND)(-2),
                0,
                0,
                0,
                0,
                Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE
                | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE
                | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

            Windows.Win32.PInvoke.SetWindowPos(
                (HWND)Handle,
                (HWND)(-1),
                workingArea.Left,
                workingArea.Top,
                contentSize.Width,
                contentSize.Height,
                Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }

        private unsafe static HMONITOR GetMonitorForScreen(Screen screen)
        {
            HMONITOR monitor = default;
            var fieldInfo = typeof(Screen).GetField("_hmonitor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (fieldInfo != null)
            {
                var value = fieldInfo.GetValue(screen)!;
                monitor = Unsafe.As<HMONITORWrapper>(value).HMONITOR;
            }

            if (monitor.Value == 0 || monitor.Value == 0xBAADF00D)
            {
                monitor = Windows.Win32.PInvoke.MonitorFromWindow(default, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
            }
            return monitor;
        }

        class HMONITORWrapper
        {
            public HMONITOR HMONITOR;
        }
    }
}
