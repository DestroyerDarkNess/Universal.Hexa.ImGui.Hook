using Hexa.NET.ImGui;
using RenderSpy.Globals;
using RenderSpy.Inputs;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Universal.DearImGui.Hook.Backends;
using Universal.DearImGui.Hook.Core;

namespace Universal.DearImGui.Hook
{
    public class dllmain
    {
        public static IntPtr GameHandle = IntPtr.Zero;
        public static bool Show = false;
        public static bool Logger = true;
        public static bool Runtime = true;
        public static Size Gui_Size = new System.Drawing.Size(800, 600);

        // ImGui Backend
        public static IImGuiBackend ImGuiBackend = null;

        public static InputImguiEmu InputImguiEmu = null;

        public static void Main(string[] args)
        {
            Show = true;

            bool result = Diagnostic.RunDiagnostic();

            if (result)
            {
                Console.WriteLine("All diagnostics passed. The system is ready.");
            }
            else
            {
                Console.WriteLine("Some diagnostics failed. Please resolve the missing libraries, Press any key to continue.");
                Console.ReadKey();
            }

            using (RenderSpy.Overlay.D3D9Window OverlayWindow = new RenderSpy.Overlay.D3D9Window() { ResizableBorders = true, ShowInTaskbar = true })
            {
                OverlayWindow.Text = "Universal ImGui Hook";
                OverlayWindow.TransparencyKey = Color.Purple;
                OverlayWindow.BackColor = Color.DarkGray;
                OverlayWindow.ClearColor = new SharpDX.Color(OverlayWindow.BackColor.R, OverlayWindow.BackColor.G, OverlayWindow.BackColor.B, OverlayWindow.BackColor.A);
                //OverlayWindow.AdditionalExStyle = 0x80000 | 0x00000080; // WS_EX_LAYERED + WS_EX_TOOLWINDOW
                OverlayWindow.FormBorderStyle = FormBorderStyle.FixedSingle;
                OverlayWindow.Size = Gui_Size;
                OverlayWindow.StartPosition = FormStartPosition.CenterScreen;

                Size clientSize = OverlayWindow.ClientSize;
                OverlayWindow.PresentParams = new SharpDX.Direct3D9.PresentParameters
                {
                    Windowed = true,
                    SwapEffect = SharpDX.Direct3D9.SwapEffect.Discard,
                    BackBufferFormat = SharpDX.Direct3D9.Format.A8R8G8B8,
                    PresentationInterval = SharpDX.Direct3D9.PresentInterval.One,
                    BackBufferWidth = clientSize.Width,
                    BackBufferHeight = clientSize.Height,
                    EnableAutoDepthStencil = true,
                    AutoDepthStencilFormat = SharpDX.Direct3D9.Format.D16,
                };

                OverlayWindow.OnD3DReady += (sender, e) =>
                {
                    while (OverlayWindow.Visible == false) Application.DoEvents();
                    GameHandle = OverlayWindow.Handle;
                    Core.WinApi.Interactive(OverlayWindow.Handle, true);
                    try
                    {
                        var A = Core.Cimgui.CimguiRuntime.Handle;
                    }
                    catch
                    {
                        throw new Exception("Failed to load cimgui.dll");
                    }

                    ImGuiBackend = new D3D9Backend();
                    ImGuiBackend.OnImGuiCreateContext += HandleImGuiCreateContext;
                    ImGuiBackend.OnImGuiRender += HandleImGuiRender;

                    ImGuiBackend.Initialize(OverlayWindow.D3DDevice.NativePointer, OverlayWindow.Handle);

                    RenderSpy.Graphics.d3d9.EndScene EndSceneHook_9 = new RenderSpy.Graphics.d3d9.EndScene();
                    EndSceneHook_9.Install();

                    EndSceneHook_9.EndSceneEvent += (IntPtr device) =>
                    {
                        try
                        {
                            if (ImGuiBackend != null)
                            {
                                ImGuiBackend.NewFrame();
                                ImGuiBackend.Render();
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error in EndScene Event: {ex.Message} {Environment.NewLine} {Environment.NewLine} {ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        return EndSceneHook_9.EndScene_orig(device);
                    };

                    RenderSpy.Graphics.d3d9.Reset ResetHook_9 = new RenderSpy.Graphics.d3d9.Reset();
                    ResetHook_9.Install();

                    ResetHook_9.Reset_Event += (IntPtr device, ref SharpDX.Direct3D9.PresentParameters presentParameters) =>
                    {
                        if (ImGuiBackend != null) ImGuiBackend.OnLostDevice();

                        int Reset = ResetHook_9.Reset_orig(device, ref presentParameters);

                        if (ImGuiBackend != null) ImGuiBackend.OnResetDevice();

                        return Reset;
                    };
                };

                try { Application.Run(OverlayWindow); } catch (Exception Ex) { System.Windows.Forms.MessageBox.Show(Ex.Message); Environment.Exit(0); }
            }
        }

        public static void EntryPoint()
        {
            while (GameHandle.ToInt32() == 0)
            {
                GameHandle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;  // Get Main Game Window Handle
            }

            if (Logger == true)
            {
                RenderSpy.Globals.WinApi.AllocConsole();
                bool result = Diagnostic.RunDiagnostic();

                if (result)
                {
                    Console.WriteLine("All diagnostics passed. The system is ready.");
                }
                else
                {
                    Console.WriteLine("Some diagnostics failed. Please resolve the missing libraries, Press any key to continue.");
                    Console.ReadKey();
                }
            }

            try
            {
                var A = Core.Cimgui.CimguiRuntime.Handle;
            }
            catch
            {
                return;
            }

            RenderSpy.Graphics.GraphicsType GraphicsT = RenderSpy.Graphics.GraphicsType.d3d9; // RenderSpy.Graphics.Detector.GetCurrentGraphicsType();

            List<RenderSpy.Interfaces.IHook> CurrentHooks = new List<RenderSpy.Interfaces.IHook>();

            Console.WriteLine("Current Graphics: " + GraphicsT.ToString() + " LIB: " + RenderSpy.Graphics.Detector.GetLibByEnum(GraphicsT));

            switch (GraphicsT)
            {
                case RenderSpy.Graphics.GraphicsType.d3d9:

                    RenderSpy.Graphics.d3d9.Present PresentHook_9 = new RenderSpy.Graphics.d3d9.Present();
                    PresentHook_9.Install();
                    CurrentHooks.Add(PresentHook_9);
                    PresentHook_9.PresentEvent += (IntPtr device, IntPtr sourceRect, IntPtr destRect, IntPtr hDestWindowOverride, IntPtr dirtyRegion) =>
                    {
                        try
                        {
                            if (ImGuiBackend == null)
                            {
                                ImGuiBackend = new D3D9Backend();
                                ImGuiBackend.OnImGuiCreateContext += HandleImGuiCreateContext;
                                ImGuiBackend.OnImGuiRender += HandleImGuiRender;

                                ImGuiBackend.Initialize(device, dllmain.GameHandle);
                            }

                            ImGuiBackend.NewFrame();
                            ImGuiBackend.Render();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error in Present Event: {ex.Message} {Environment.NewLine} {Environment.NewLine} {ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        return PresentHook_9.Present_orig(device, sourceRect, destRect, hDestWindowOverride, dirtyRegion);
                    };

                    RenderSpy.Graphics.d3d9.Reset ResetHook_9 = new RenderSpy.Graphics.d3d9.Reset();
                    ResetHook_9.Install();
                    CurrentHooks.Add(ResetHook_9);
                    ResetHook_9.Reset_Event += (IntPtr device, ref SharpDX.Direct3D9.PresentParameters presentParameters) =>
                    {
                        if (ImGuiBackend != null) ImGuiBackend.OnLostDevice();

                        int Reset = ResetHook_9.Reset_orig(device, ref presentParameters);

                        if (ImGuiBackend != null) ImGuiBackend.OnResetDevice();

                        return Reset;
                    };

                    break;

                case RenderSpy.Graphics.GraphicsType.d3d10:

                    RenderSpy.Graphics.d3d10.Present PresentHook_10 = new RenderSpy.Graphics.d3d10.Present();
                    PresentHook_10.Install();
                    CurrentHooks.Add(PresentHook_10);
                    PresentHook_10.PresentEvent += (swapChainPtr, syncInterval, flags) =>
                    {
                        return PresentHook_10.Present_orig(swapChainPtr, syncInterval, flags);
                    };

                    break;

                case RenderSpy.Graphics.GraphicsType.d3d11:

                    RenderSpy.Graphics.d3d11.Present PresentHook_11 = new RenderSpy.Graphics.d3d11.Present();
                    PresentHook_11.Install();
                    CurrentHooks.Add(PresentHook_11);
                    PresentHook_11.PresentEvent += (swapChainPtr, syncInterval, flags) =>
                    {
                        return PresentHook_11.Present_orig(swapChainPtr, syncInterval, flags);
                    };

                    break;

                case RenderSpy.Graphics.GraphicsType.d3d12:

                    break;

                case RenderSpy.Graphics.GraphicsType.opengl:

                    RenderSpy.Graphics.opengl.wglSwapBuffers glSwapBuffersHook = new RenderSpy.Graphics.opengl.wglSwapBuffers();
                    glSwapBuffersHook.Install();
                    CurrentHooks.Add(glSwapBuffersHook);
                    glSwapBuffersHook.wglSwapBuffersEvent += (IntPtr hdc) =>
                    {
                        try
                        {
                            if (ImGuiBackend == null)
                            {
                                ImGuiBackend = new OpenGLBackend();
                                ImGuiBackend.OnImGuiCreateContext += HandleImGuiCreateContext;
                                ImGuiBackend.OnImGuiRender += HandleImGuiRender;

                                ImGuiBackend.Initialize(hdc, dllmain.GameHandle);
                            }

                            ImGuiBackend.NewFrame();
                            ImGuiBackend.Render();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error in OpenGL Present Event: {ex.Message} {Environment.NewLine} {Environment.NewLine} {ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        return glSwapBuffersHook.wglSwapBuffers_orig(hdc); ;
                    };

                    break;

                case RenderSpy.Graphics.GraphicsType.vulkan:

                    break;

                default:

                    break;
            }

            DirectInputHook DirectInputHook_Hook = new DirectInputHook();

            IntPtr already = RenderSpy.Globals.WinApi.GetModuleHandle("dinput8.dll");

            if (already != IntPtr.Zero)
            {
                DirectInputHook_Hook.WindowHandle = GameHandle;
                DirectInputHook_Hook.Install();
                DirectInputHook_Hook.GetDeviceState += (IntPtr hDevice, int cbData, IntPtr lpvData) =>
                {
                    if (Show) return 0;
                    return DirectInputHook_Hook.Hook_orig(hDevice, cbData, lpvData);
                };
            }

            SetCursorPos NewHookCursor = new SetCursorPos();
            NewHookCursor.Install();
            NewHookCursor.SetCursorPos_Event += (int x, int y) =>
            {
                NewHookCursor.BlockInput = Show;
                return false;
            };

            while (Runtime) { }

            if (already != IntPtr.Zero) DirectInputHook_Hook.Uninstall();
            NewHookCursor.Uninstall();
            foreach (var hook in CurrentHooks)
            {
                if (hook != null) hook.Uninstall();
            }

            if (ImGuiBackend != null) ImGuiBackend.Dispose();
        }

        public static void HandleImGuiCreateContext()
        {
            var style = ImGui.GetStyle();
            var colors = style.Colors;

            colors[(int)ImGuiCol.Text] = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.50f, 0.50f, 1.00f);
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.10f, 0.10f, 0.10f, 1.00f);
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.19f, 0.19f, 0.19f, 0.92f);
            colors[(int)ImGuiCol.Border] = new Vector4(0.19f, 0.19f, 0.19f, 0.29f);
            colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.24f);
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.05f, 0.05f, 0.05f, 0.54f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.19f, 0.19f, 0.19f, 0.54f);
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.20f, 0.22f, 0.23f, 1.00f);
            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.06f, 0.06f, 0.06f, 1.00f);
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.06f, 0.06f, 0.06f, 1.00f);
            colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.06f, 0.06f, 0.06f, 1.00f);
            colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.05f, 0.05f, 0.05f, 0.54f);
            colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.34f, 0.34f, 0.34f, 0.54f);
            colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.40f, 0.40f, 0.40f, 0.54f);
            colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.56f, 0.56f, 0.56f, 0.54f);
            colors[(int)ImGuiCol.CheckMark] = new Vector4(0.33f, 0.67f, 0.86f, 1.00f);
            colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.34f, 0.34f, 0.34f, 0.54f);
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.56f, 0.56f, 0.56f, 0.54f);
            colors[(int)ImGuiCol.Button] = new Vector4(0.05f, 0.05f, 0.05f, 0.54f);
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.19f, 0.19f, 0.19f, 0.54f);
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.20f, 0.22f, 0.23f, 1.00f);
            colors[(int)ImGuiCol.Header] = new Vector4(0.00f, 0.00f, 0.00f, 0.52f);
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.00f, 0.00f, 0.00f, 0.36f);
            colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.20f, 0.22f, 0.23f, 0.33f);
            colors[(int)ImGuiCol.Separator] = new Vector4(0.28f, 0.28f, 0.28f, 0.29f);
            colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.44f, 0.44f, 0.44f, 0.29f);
            colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.40f, 0.44f, 0.47f, 1.00f);
            colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.28f, 0.28f, 0.28f, 0.29f);
            colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.44f, 0.44f, 0.44f, 0.29f);
            colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.40f, 0.44f, 0.47f, 1.00f);
            colors[(int)ImGuiCol.Tab] = new Vector4(0.00f, 0.00f, 0.00f, 0.52f);
            colors[(int)ImGuiCol.TabHovered] = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);
            colors[(int)ImGuiCol.TabSelected] = new Vector4(0.20f, 0.20f, 0.20f, 0.36f);
            colors[(int)ImGuiCol.TabDimmed] = new Vector4(0.00f, 0.00f, 0.00f, 0.52f);
            colors[(int)ImGuiCol.TabDimmedSelected] = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);
            colors[(int)ImGuiCol.DockingPreview] = new Vector4(0.33f, 0.67f, 0.86f, 1.00f);
            colors[(int)ImGuiCol.DockingEmptyBg] = new Vector4(1.00f, 0.00f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.PlotLines] = new Vector4(1.00f, 0.00f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(1.00f, 0.00f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogram] = new Vector4(1.00f, 0.00f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(1.00f, 0.00f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.TableHeaderBg] = new Vector4(0.00f, 0.00f, 0.00f, 0.52f);
            colors[(int)ImGuiCol.TableBorderStrong] = new Vector4(0.00f, 0.00f, 0.00f, 0.52f);
            colors[(int)ImGuiCol.TableBorderLight] = new Vector4(0.28f, 0.28f, 0.28f, 0.29f);
            colors[(int)ImGuiCol.TableRowBg] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            colors[(int)ImGuiCol.TableRowBgAlt] = new Vector4(1.00f, 1.00f, 1.00f, 0.06f);
            colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.20f, 0.22f, 0.23f, 1.00f);
            colors[(int)ImGuiCol.DragDropTarget] = new Vector4(0.33f, 0.67f, 0.86f, 1.00f);
            colors[(int)ImGuiCol.NavCursor] = new Vector4(1.00f, 0.00f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(1.00f, 0.00f, 0.00f, 0.70f);
            colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(1.00f, 0.00f, 0.00f, 0.20f);
            colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.10f, 0.10f, 0.10f, 0.00f);

            style.WindowPadding = new Vector2(8.00f, 8.00f);
            style.FramePadding = new Vector2(5.00f, 2.00f);
            style.CellPadding = new Vector2(6.00f, 6.00f);
            style.ItemSpacing = new Vector2(6.00f, 6.00f);
            style.ItemInnerSpacing = new Vector2(6.00f, 6.00f);
            style.TouchExtraPadding = new Vector2(0.00f, 0.00f);
            style.IndentSpacing = 25;
            style.ScrollbarSize = 15;
            style.GrabMinSize = 10;
            style.WindowBorderSize = 1;
            style.ChildBorderSize = 1;
            style.PopupBorderSize = 1;
            style.TabBorderSize = 1;
            style.ChildRounding = 4;
            style.PopupRounding = 4;
            style.ScrollbarRounding = 9;
            style.GrabRounding = 3;
            style.LogSliderDeadzone = 4;
            style.TabRounding = 4;
            //style.WindowRounding = 5.0f;
            //style.FrameRounding = 5.0f;

            // When viewports are enabled we tweak WindowRounding/WindowBg so platform windows can look identical to regular ones.
            if ((ImGuiBackend.IO.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
            {
                style.WindowRounding = 0.0f;
                style.Colors[(int)ImGuiCol.WindowBg].W = 1.0f;
            }

            InputImguiEmu = new InputImguiEmu(ImGuiBackend.IO);

            // Menu Key
            InputImguiEmu.AddEvent(Keys.F2, () =>
            {
                Show = !Show;
            });

            //Panic Key
            InputImguiEmu.AddEvent(Keys.F3, () =>
            {
                Runtime = false;
            });
        }

        public static void HandleImGuiRender()
        {
            if (!Runtime) return;

            ImGuiBackend.IO.MouseDrawCursor = Show;

            if (InputImguiEmu != null)
            {
                InputImguiEmu.Enabled = Show;
                InputImguiEmu.UpdateKeyboardState();
            }

            if (Show)
            {
                if (InputImguiEmu != null) InputImguiEmu.UpdateMouseState();
                ImGui.ShowDemoWindow();
                ImGui.Begin("Universal Demo", ref Show);
                ImGui.Text("Hello from C#");
                ImGui.End();
            }
        }
    }
}