using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.D3D9;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.ImGui.Backends.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using ImGui = Hexa.NET.ImGui.ImGui;

namespace Universal.DearImGui.Hook.Backends
{
    public sealed unsafe class OpenGLBackend : ImGuiBackendBase
    {
        private bool _frameStarted = false;
        private string tmpDir, cimguiPath, _implPath;
        private IntPtr cimguiHandle = IntPtr.Zero, implHandle = IntPtr.Zero, _windowHandle = IntPtr.Zero;
        private string _glslVersion = "#version 130"; // Default GLSL version, can be customized

        public override bool Initialize(IntPtr devicePtr, IntPtr windowHandle)
        {
            if (IsInitialized) return true;

            _windowHandle = windowHandle;

            // Create ImGui Context
            Context = ImGui.CreateContext();
            ImGui.SetCurrentContext(Context);
            IO = ImGui.GetIO();

            // Raise context creation event
            RaiseOnCreateContext();

            // Initialize Win32 backend
            ImGuiImplWin32.SetCurrentContext(Context);
            ImGuiImplWin32.Init(windowHandle);

            // Initialize OpenGL3 backend
            ImGuiImplOpenGL3.SetCurrentContext(Context);
            if (!ImGuiImplOpenGL3.Init(_glslVersion))
            {
                // Cleanup on failure
                ImGuiImplWin32.Shutdown();
                ImGui.DestroyContext(Context);
                return false;
            }

            IsInitialized = true;
            return IsInitialized;
        }

        public override void NewFrame()
        {
            if (!IsInitialized) return;
            if (_frameStarted) return;

            // Start new frame for both backends
            ImGuiImplOpenGL3.NewFrame();
            ImGuiImplWin32.NewFrame();
            ImGui.NewFrame();

            _frameStarted = true;
        }

        public override void Render()
        {
            if (!IsInitialized) return;
            if (!_frameStarted) return;

            // Raise render event for custom rendering
            RaiseRender();

            // Render ImGui
            ImGui.Render();

            // Handle multi-viewport if enabled
            if ((IO.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
            {
                ImGui.UpdatePlatformWindows();
                ImGui.RenderPlatformWindowsDefault();
            }

            // Render draw data
            ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

            _frameStarted = false;
        }

        /// <summary>
        /// Sets the GLSL version string to use. Must be called before Initialize().
        /// </summary>
        /// <param name="glslVersion">GLSL version string (e.g., "#version 130", "#version 330", etc.)</param>
        public void SetGLSLVersion(string glslVersion)
        {
            if (IsInitialized)
                throw new InvalidOperationException("Cannot change GLSL version after initialization");

            _glslVersion = glslVersion ?? "#version 130";
        }

        /// <summary>
        /// Called when OpenGL context is lost (not typically needed for OpenGL, but kept for consistency)
        /// </summary>
        public override void OnLostDevice()
        {
            if (!IsInitialized) return;
            // OpenGL doesn't typically have device loss like D3D9, but we keep this for interface consistency
            // If needed, custom cleanup can be added here
        }

        /// <summary>
        /// Called when OpenGL context is restored (not typically needed for OpenGL, but kept for consistency)
        /// </summary>
        public override void OnResetDevice()
        {
            if (!IsInitialized) return;
            // OpenGL doesn't typically have device reset like D3D9, but we keep this for interface consistency
            // If needed, custom reinitialization can be added here
        }

        public override void Dispose()
        {
            if (!IsInitialized) return;

            try
            {
                // Shutdown ImGui backends
                ImGuiImplOpenGL3.Shutdown();
                ImGuiImplWin32.Shutdown();

                // Destroy ImGui context
                ImGui.DestroyContext(Context);

                // Free loaded libraries if any
                void FreeIfLoaded(ref IntPtr handle)
                {
                    if (handle != IntPtr.Zero)
                    {
                        RenderSpy.Globals.WinApi.FreeLibrary(handle);
                        handle = IntPtr.Zero;
                    }
                }

                FreeIfLoaded(ref implHandle);
                FreeIfLoaded(ref cimguiHandle);

                // Clean up temporary directory if it exists
                try
                {
                    if (Directory.Exists(tmpDir))
                        Directory.Delete(tmpDir, true);
                }
                catch
                {
                    // Swallow exception - temporary directory cleanup is not critical
                }
            }
            finally
            {
                IsInitialized = false;
                _frameStarted = false;
                _windowHandle = IntPtr.Zero;
            }
        }
    }
}