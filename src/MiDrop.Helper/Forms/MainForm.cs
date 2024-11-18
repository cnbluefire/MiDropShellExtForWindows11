using System;
using System.Collections.Generic;
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

using WINDOW_STYLE = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE;
using WINDOW_EX_STYLE = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE;

namespace MiDrop.Helper.Forms
{
    public class MainForm : Form
    {
        private const int WindowWidth = 120;
        private const int WindowHeight = 10;

        private const float GeometryMaxWidth = WindowWidth - 10;
        private const float GeometryMaxHeight = 40;
        private const float GeometryWidth = GeometryMaxWidth;
        private const float GeometryNormalHeight = WindowHeight - 10;
        private const float GeometryDragOverHeight = WindowHeight - 2;

        private DesktopWindowTarget? desktopWindowTarget;
        private SpriteVisual? rootVisual;
        private ShapeVisual? shapeVisual;

        private HttpClient? httpClient;

        public MainForm()
        {
            this.AllowDrop = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;

            ClientSize = default;

            var weakThis = new WeakReference(this);
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            void OnDisplaySettingsChanged(object? sender, EventArgs e)
            {
                if (weakThis.Target is MainForm strongThis)
                {
                    SetBounds(WindowWidth, WindowHeight);
                    UpdateRootVisualSize();
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

        protected override void OnDragOver(DragEventArgs drgevent)
        {
            base.OnDragOver(drgevent);
            drgevent.Effect = DragDropEffects.Copy;

            if (shapeVisual != null)
            {
                shapeVisual.Offset = GetShapeVisualOffset(true);
            }
        }

        protected override void OnDragLeave(EventArgs e)
        {
            base.OnDragLeave(e);

            if (shapeVisual != null)
            {
                shapeVisual.Offset = GetShapeVisualOffset(false);
            }

            SetBounds(WindowWidth, WindowHeight);
            UpdateRootVisualSize();
        }

        protected override async void OnDragDrop(DragEventArgs drgevent)
        {
            base.OnDragDrop(drgevent);

            if (shapeVisual != null)
            {
                shapeVisual.Offset = GetShapeVisualOffset(false);
            }

            var data = drgevent.Data;
            if (data != null)
            {
                if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    if (data.GetData(DataFormats.FileDrop) is string[] files)
                    {
                        await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync(default);
                        await MiDrop.Core.XiaomiPcManagerHelper.SendFilesAsync(files, TimeSpan.FromSeconds(5));
                    }
                }
                else if (data.GetDataPresent("text/x-moz-url"))
                {
                    if (data.GetData("text/x-moz-url") is MemoryStream memoryStream)
                    {
                        using (memoryStream)
                        {
                            var parts = Encoding.Unicode.GetString(memoryStream.ToArray()).Split((char)10);
                            if (parts.Length > 0
                                && Uri.TryCreate(parts[0], UriKind.Absolute, out var uri))
                            {
                                var launch = await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync(default);
                                if (launch)
                                {
                                    var fileName = System.IO.Path.GetFileName(uri.LocalPath);
                                    if (string.IsNullOrEmpty(System.IO.Path.GetExtension(fileName)))
                                    {
                                        if (data.GetDataPresent("FileGroupDescriptorW")
                                            && data.GetData("FileGroupDescriptorW") is MemoryStream fileGroupDescriptorStream)
                                        {
                                            using (fileGroupDescriptorStream)
                                            {
                                                var names = GetFileNames(fileGroupDescriptorStream);
                                                if (names != null && names.Length > 0) fileName = names[0];
                                            }
                                        }
                                    }

                                    if (httpClient == null) httpClient = new HttpClient();

                                    try
                                    {
                                        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                                        var stream = await httpClient.GetStreamAsync(uri, cts.Token);

                                        var tempFolder = GetTempFolder();
                                        var fullName = System.IO.Path.Combine(tempFolder, fileName);
                                        using (var fs = new FileStream(fullName, FileMode.Create, FileAccess.Write))
                                        {
                                            await stream.CopyToAsync(fs);
                                        }

                                        await MiDrop.Core.XiaomiPcManagerHelper.SendFilesAsync([fullName], TimeSpan.FromSeconds(5));
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
                else if (data.GetDataPresent("FileGroupDescriptorW")
                    && data.GetDataPresent("FileContents"))
                {
                    if (data.GetData("FileGroupDescriptorW") is MemoryStream fileGroupDescriptorStream)
                    {
                        using (fileGroupDescriptorStream)
                        {
                            var names = GetFileNames(fileGroupDescriptorStream);

                            var fileContents = (MemoryStream[]?)data.GetData("FileContents");

                            if (names.Length > 0 && fileContents != null && fileContents.Length == names.Length)
                            {
                                var launch = await MiDrop.Core.XiaomiPcManagerHelper.LaunchAsync(default);
                                if (launch)
                                {
                                    var tempFolder = GetTempFolder();

                                    var fullPaths = new List<string>();
                                    var nameDict = new Dictionary<string, int>();
                                    for (int i = 0; i < names.Length; i++)
                                    {
                                        using (fileContents[i])
                                        {
                                            if (nameDict.TryGetValue(names[i], out var v)) v++;
                                            else v = 0;
                                            nameDict[names[i]] = v;
                                            if (v > 0)
                                            {
                                                names[i] = $"{names[i]} ({v})";
                                            }

                                            try
                                            {
                                                var fileName = System.IO.Path.Combine(tempFolder, names[i]);
                                                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                                                {
                                                    await fileContents[i].CopyToAsync(fs);
                                                    fullPaths.Add(fileName);
                                                }
                                            }
                                            catch { }
                                        }
                                    }

                                    if (fullPaths.Count > 0)
                                    {
                                        await MiDrop.Core.XiaomiPcManagerHelper.SendFilesAsync([.. fullPaths], TimeSpan.FromSeconds(5));
                                    }
                                }
                            }
                        }
                    }
                }
                else if (data.GetDataPresent(DataFormats.Text))
                {
                    if (data.GetData(DataFormats.Text) is string text)
                    {
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
            }
        }

        private unsafe static string[] GetFileNames(MemoryStream fileGroupDescriptorStream)
        {
            var fileGroupDescriptorBytes = fileGroupDescriptorStream.ToArray();

            fixed (void* _pFileGroupDescriptor = fileGroupDescriptorBytes)
            {
                var pFileGroupDescriptor = (Windows.Win32.UI.Shell.FILEGROUPDESCRIPTORW*)_pFileGroupDescriptor;

                var count = (int)(pFileGroupDescriptor->cItems);

                var names = new string[count];

                var span = pFileGroupDescriptor->fgd.AsSpan(count);
                for (int i = 0; i < count; i++)
                {
                    names[i] = span[i].cFileName.ToString();
                }

                return names;
            }
        }

        private static string GetTempFolder()
        {
            var tmpFolder = System.IO.Path.Combine(Path.GetTempPath(), "MiDrop.Helper");
            if (!Directory.Exists(tmpFolder)) Directory.CreateDirectory(tmpFolder);

            var subFolder = "";

            do
            {
                subFolder = System.IO.Path.Combine(tmpFolder, $"{Guid.NewGuid():N}"[..8]);
            } while (Directory.Exists(subFolder));
            Directory.CreateDirectory(subFolder);

            return subFolder;
        }


        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            if (desktopWindowTarget != null)
            {
                desktopWindowTarget?.Dispose();
                desktopWindowTarget = null;
            }

            base.OnHandleCreated(e);

            desktopWindowTarget = CompositionHelper.CreateDesktopWindowTarget(Handle, true);
            if (desktopWindowTarget != null)
            {
                rootVisual = CompositionHelper.Compositor.CreateSpriteVisual();
                rootVisual.RelativeSizeAdjustment = new System.Numerics.Vector2(1, 1);
                rootVisual.Clip = CompositionHelper.Compositor.CreateInsetClip();

                desktopWindowTarget.Root = rootVisual;

                var geomertry = CompositionHelper.Compositor.CreateRoundedRectangleGeometry();
                geomertry.Size = new System.Numerics.Vector2(GeometryWidth, GeometryMaxHeight);
                geomertry.CornerRadius = new System.Numerics.Vector2(4);

                var shape = CompositionHelper.Compositor.CreateSpriteShape(geomertry);
                shape.FillBrush = CompositionHelper.Compositor.CreateColorBrush(Windows.UI.Color.FromArgb(220, 255, 255, 255));
                shape.StrokeBrush = CompositionHelper.Compositor.CreateColorBrush(Windows.UI.Color.FromArgb(80, 0, 0, 0));
                shape.StrokeThickness = 1f;

                shapeVisual = CompositionHelper.Compositor.CreateShapeVisual();
                shapeVisual.Size = new System.Numerics.Vector2(GeometryWidth, GeometryMaxHeight);
                shapeVisual.Offset = GetShapeVisualOffset(false);
                shapeVisual.Shapes.Add(shape);

                var offsetAnimation = CompositionHelper.Compositor.CreateVector3KeyFrameAnimation();
                offsetAnimation.InsertExpressionKeyFrame(1f, "this.FinalValue");
                offsetAnimation.Duration = TimeSpan.FromSeconds(0.2);
                offsetAnimation.Target = "Offset";

                var imp = CompositionHelper.Compositor.CreateImplicitAnimationCollection();
                imp[offsetAnimation.Target] = offsetAnimation;
                shapeVisual.ImplicitAnimations = imp;

                rootVisual.Children.InsertAtTop(shapeVisual);

                SetBounds(WindowWidth, WindowHeight);
                UpdateRootVisualSize();
            }
        }

        private static System.Numerics.Vector3 GetShapeVisualOffset(bool isDragOver)
        {
            var height = isDragOver ? GeometryDragOverHeight : GeometryNormalHeight;
            return new System.Numerics.Vector3((WindowWidth - GeometryWidth) / 2, -GeometryMaxHeight + height, 0);
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);

            SetBounds(WindowWidth, WindowHeight);
            UpdateRootVisualSize();
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

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            desktopWindowTarget?.Dispose();
            desktopWindowTarget = null;
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
