using Silk.NET.Core;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;

namespace SilkTriangleApp;

/// <summary>
/// Manages window creation, lifecycle, and rendering for multiple renderers in a thread-safe manner.
/// </summary>
/// <remarks>
/// The WindowManager is responsible for creating and managing the main application window,
/// handling window events, and coordinating rendering across multiple IRenderer instances.
/// It runs the window on a separate thread to prevent blocking the main application thread,
/// allowing for responsive UI and background processing.
/// 
/// The manager supports multiple renderers that can be added dynamically, enabling
/// complex rendering scenarios such as combining 2D graphics, 3D models, and GUI elements.
/// Each renderer is initialized with the window and OpenGL context, and their render
/// methods are called in the order they were added during each frame.
/// 
/// The WindowManager provides events for external monitoring of window lifecycle:
/// - OnWindowLoad: Fired when the window is loaded and ready
/// - OnWindowRender: Fired each frame during rendering
/// - OnWindowClose: Fired when the window is closing
/// 
/// This class implements the IDisposable pattern to ensure proper cleanup of
/// window resources and renderer disposal when the application shuts down.
/// </remarks>
/// <example>
/// <code>
/// // Create a window manager and add renderers
/// var windowManager = new WindowManager();
/// windowManager.AddRenderer(new Renderer2D());
/// windowManager.AddRenderer(new RendererGUI());
/// 
/// // Create and run the window (non-blocking)
/// windowManager.CreateWindow();
/// windowManager.Run();
/// 
/// // Application continues running on main thread
/// Console.WriteLine("Window is running in background!");
/// 
/// // Clean up when done
/// windowManager.Dispose();
/// </code>
/// </example>
/// <seealso cref="IRenderer"/>
/// <seealso cref="Renderer2D"/>
/// <seealso cref="RendererGUI"/>

public class WindowManager : IDisposable
{
    /// <summary>
    /// The main application window managed by this WindowManager.
    /// </summary>
    /// <remarks>
    /// This window is created using Silk.NET's windowing system and serves as the
    /// primary rendering surface for all attached renderers. The window is configured
    /// with OpenGL context and input handling capabilities.
    /// </remarks>
    private IWindow? _window;

    /// <summary>
    /// The OpenGL context associated with the window.
    /// </summary>
    /// <remarks>
    /// This GL instance is obtained from the window and shared among all renderers
    /// for OpenGL operations. It's initialized when the window is loaded and
    /// remains valid throughout the window's lifetime.
    /// </remarks>
    private GL? _gl;

    /// <summary>
    /// Collection of renderers that will be rendered each frame.
    /// </summary>
    /// <remarks>
    /// Renderers are stored in the order they were added and will be rendered
    /// in that same order during each frame. This allows for proper layering
    /// of different rendering elements (e.g., 2D background, 3D models, GUI overlay).
    /// 
    /// The collection can be modified while the window is running, though it's
    /// recommended to add all renderers before starting the window for optimal performance.
    /// </remarks>
    private List<IRenderer> _renderers = new();

    /// <summary>
    /// Thread that runs the window event loop.
    /// </summary>
    /// <remarks>
    /// The window runs on a separate thread to prevent blocking the main application
    /// thread. This allows the application to continue processing while the window
    /// handles rendering and user input in the background.
    /// </remarks>
    private Thread? _windowThread;

    /// <summary>
    /// Flag indicating whether the window is currently running.
    /// </summary>
    /// <remarks>
    /// This flag prevents multiple start attempts and helps track the window's
    /// running state. It's set to true when Run() is called and false when
    /// the window is closed or disposed.
    /// </remarks>
    private bool _isRunning;

    /// <summary>
    /// Flag indicating whether the window manager has been disposed.
    /// </summary>
    /// <remarks>
    /// This flag prevents multiple disposal attempts and ensures that resources
    /// are only cleaned up once. It's checked in various methods to prevent
    /// operations on disposed objects.
    /// </remarks>
    private bool _isDisposed;

    /// <summary>
    /// Event fired when the window is loaded and ready for rendering.
    /// </summary>
    /// <remarks>
    /// This event is triggered after the window has been created and the OpenGL
    /// context has been initialized. It's a good place to perform any additional
    /// setup that depends on the window being fully ready.
    /// </remarks>
    public event Action? OnWindowLoad;

    /// <summary>
    /// Event fired each frame during the rendering process.
    /// </summary>
    /// <remarks>
    /// This event is triggered every frame with the delta time since the last frame.
    /// It can be used for monitoring rendering performance or implementing
    /// frame-based logic that needs to run alongside the renderers.
    /// </remarks>
    public event Action<double>? OnWindowRender;

    /// <summary>
    /// Event fired when the window is about to close.
    /// </summary>
    /// <remarks>
    /// This event is triggered when the user closes the window or when the
    /// window is programmatically closed. It provides an opportunity to perform
    /// cleanup operations before the window is fully disposed.
    /// </remarks>
    public event Action? OnWindowClose;

    /// <summary>
    /// Adds a renderer to the window manager's rendering pipeline.
    /// </summary>
    /// <param name="renderer">The renderer to add to the pipeline.</param>
    /// <remarks>
    /// This method adds a renderer to the internal collection. The renderer will be
    /// initialized when the window is loaded and will be rendered each frame in
    /// the order it was added.
    /// 
    /// Renderers should be added before calling CreateWindow() for optimal performance,
    /// though they can be added at any time during the window's lifetime.
    /// 
    /// The renderer will receive the window and OpenGL context during initialization,
    /// allowing it to set up its rendering resources and shaders.
    /// </remarks>
    /// <example>
    /// <code>
    /// var windowManager = new WindowManager();
    /// windowManager.AddRenderer(new Renderer2D());
    /// windowManager.AddRenderer(new RendererGUI());
    /// </code>
    /// </example>
    public void AddRenderer(IRenderer renderer)
    {
        _renderers.Add(renderer);
    }

    /// <summary>
    /// Removes a renderer from the window manager's rendering pipeline.
    /// </summary>
    /// <param name="renderer">The renderer to remove from the pipeline.</param>
    /// <remarks>
    /// This method removes a renderer from the internal collection. The renderer
    /// will no longer be rendered in subsequent frames.
    /// 
    /// Note that this method does not dispose of the renderer - that should be
    /// done explicitly if needed. The renderer will be disposed automatically
    /// when the window closes.
    /// </remarks>
    /// <example>
    /// <code>
    /// var renderer = new Renderer2D();
    /// windowManager.AddRenderer(renderer);
    /// // ... later ...
    /// windowManager.RemoveRenderer(renderer);
    /// </code>
    /// </example>
    public void RemoveRenderer(IRenderer renderer)
    {
        _renderers.Remove(renderer);
    }

    /// <summary>
    /// Creates the main application window with OpenGL context and input handling.
    /// </summary>
    /// <param name="title">The title to display in the window's title bar.</param>
    /// <param name="width">The initial width of the window in pixels.</param>
    /// <param name="height">The initial height of the window in pixels.</param>
    /// <remarks>
    /// This method creates the main window with the following configuration:
    /// - Window size: Specified width and height (default: 800x600)
    /// - Window title: Specified title (default: "Silk.NET Triangle Demo")
    /// - OpenGL version: 3.3 Core Profile
    /// - VSync: Enabled for smooth rendering
    /// - Window border: Resizable
    /// - Window state: Normal
    /// 
    /// The window is created but not yet shown or started. Call Run() to begin
    /// the window's event loop and start rendering.
    /// 
    /// Event handlers are automatically set up for:
    /// - Load: Initializes renderers with window and OpenGL context
    /// - Render: Calls each renderer's Render method
    /// - FramebufferResize: Updates viewport and notifies renderers
    /// - Closing: Initiates cleanup process
    /// </remarks>
    /// <example>
    /// <code>
    /// var windowManager = new WindowManager();
    /// windowManager.AddRenderer(new Renderer2D());
    /// windowManager.CreateWindow("My App", 1024, 768);
    /// </code>
    /// </example>
    public void CreateWindow(string title = "Silk.NET Triangle Demo", int width = 800, int height = 600)
    {
        // Create window options
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(width, height);
        options.Title = title;
        options.VSync = true;
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3));
        options.ShouldSwapAutomatically = true;
        options.TransparentFramebuffer = false;
        options.WindowBorder = WindowBorder.Resizable;
        options.WindowState = WindowState.Normal;

        _window = Window.Create(options);

        // Assign events
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Closing += OnClose;
        _window.FramebufferResize += OnFramebufferResize;
    }

    public void Run()
    {
        if (_window == null || _isRunning)
            return;

        _isRunning = true;
        _windowThread = new Thread(() =>
        {
            _window?.Run();
        });
        _windowThread.Start();
    }

    /// <summary>
    /// Starts the window's event loop on a separate thread.
    /// </summary>
    /// <remarks>
    /// This method starts the window's event loop on a background thread, allowing
    /// the main application thread to continue running without blocking. The window
    /// will begin rendering and responding to user input immediately.
    /// 
    /// The window thread will continue running until the window is closed or
    /// the WindowManager is disposed. The main thread can continue processing
    /// while the window runs in the background.
    /// 
    /// This non-blocking approach is particularly useful for applications that
    /// need to perform background tasks, handle network communication, or
    /// process data while displaying a graphical interface.
    /// 
    /// If the window is already running or hasn't been created, this method
    /// will return without starting a new thread.
    /// </remarks>
    /// <example>
    /// <code>
    /// var windowManager = new WindowManager();
    /// windowManager.AddRenderer(new Renderer2D());
    /// windowManager.CreateWindow();
    /// windowManager.Run(); // Window starts running in background
    /// 
    /// // Main thread continues here
    /// Console.WriteLine("Window is running!");
    /// </code>
    /// </example>

    /// <summary>
    /// Gets whether the window is currently running.
    /// </summary>
    /// <remarks>
    /// This property returns true if the window has been started and the window
    /// thread is still alive. It provides a way to check the current state of
    /// the window without directly accessing the internal flags.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (windowManager.IsRunning)
    /// {
    ///     Console.WriteLine("Window is active");
    /// }
    /// </code>
    /// </example>
    public bool IsRunning => _isRunning && _windowThread?.IsAlive == true;

    /// <summary>
    /// Waits for the window to close by joining the window thread.
    /// </summary>
    /// <remarks>
    /// This method blocks the current thread until the window thread completes.
    /// It's useful when you want the main thread to wait for the window to close
    /// before continuing with cleanup or shutdown operations.
    /// 
    /// This method will return immediately if the window thread is not running.
    /// </remarks>
    /// <example>
    /// <code>
    /// windowManager.Run();
    /// // Do other work...
    /// windowManager.WaitForWindowToClose(); // Wait for user to close window
    /// Console.WriteLine("Window closed");
    /// </code>
    /// </example>
    public void WaitForWindowToClose()
    {
        _windowThread?.Join();
    }

    /// <summary>
    /// Handles the window load event by initializing renderers and firing the OnWindowLoad event.
    /// </summary>
    /// <remarks>
    /// This method is called when the window is first created and ready. It:
    /// - Gets the OpenGL context from the window
    /// - Initializes all renderers with the window and OpenGL context
    /// - Fires the OnWindowLoad event for external subscribers
    /// </remarks>
    private void OnLoad()
    {
        if (_window != null)
        {
            _gl = GL.GetApi(_window);
            foreach (var renderer in _renderers)
            {
                renderer.Initialize(_window, _gl);
            }
        }
        OnWindowLoad?.Invoke();
    }

    /// <summary>
    /// Handles the window render event by rendering all renderers and firing the OnWindowRender event.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last frame in seconds.</param>
    /// <remarks>
    /// This method is called each frame to render the scene. It:
    /// - Renders each renderer in the order they were added
    /// - Fires the OnWindowRender event with the current delta time
    /// 
    /// The delta time parameter can be used by renderers for animation and
    /// time-based effects.
    /// </remarks>
    private void OnRender(double deltaTime)
    {
        foreach (var renderer in _renderers)
        {
            renderer.Render(deltaTime);
        }
        OnWindowRender?.Invoke(deltaTime);
    }

    /// <summary>
    /// Handles window resize events by notifying all renderers of the new size.
    /// </summary>
    /// <param name="size">The new size of the window framebuffer.</param>
    /// <remarks>
    /// This method is called when the window is resized. It notifies all renderers
    /// of the new window dimensions, allowing them to update their viewports,
    /// aspect ratios, and other size-dependent rendering parameters.
    /// 
    /// The OpenGL viewport is automatically updated by the window system, so
    /// renderers only need to handle any additional size-dependent logic.
    /// </remarks>
    private void OnFramebufferResize(Vector2D<int> size)
    {
        foreach (var renderer in _renderers)
        {
            renderer.OnFramebufferResize(size);
        }
    }

    /// <summary>
    /// Handles the window closing event by cleaning up renderers and firing the OnWindowClose event.
    /// </summary>
    /// <remarks>
    /// This method is called when the window is about to close. It:
    /// - Disposes of all renderers to clean up their OpenGL resources
    /// - Clears the renderer collection
    /// - Fires the OnWindowClose event for external subscribers
    /// 
    /// This ensures proper cleanup of all rendering resources before the window
    /// is fully closed.
    /// </remarks>
    private void OnClose()
    {
        if (!_isDisposed)
        {
            foreach (var renderer in _renderers)
            {
                renderer.Dispose();
            }
            _renderers.Clear();
            OnWindowClose?.Invoke();
        }
    }

    /// <summary>
    /// Disposes of the WindowManager and all associated resources.
    /// </summary>
    /// <remarks>
    /// This method performs the following cleanup operations:
    /// <list type="number">
    /// <item><description>Sets the disposed flag to prevent further operations</description></item>
    /// <item><description>Stops the window from running</description></item>
    /// <item><description>Closes the window</description></item>
    /// <item><description>Waits for the window thread to complete (with timeout)</description></item>
    /// <item><description>Disposes of the window</description></item>
    /// </list>
    /// 
    /// This method implements the IDisposable pattern and should be called
    /// when the WindowManager is no longer needed to prevent resource leaks.
    /// 
    /// The method is safe to call multiple times - subsequent calls will
    /// have no effect due to the disposed flag check.
    /// </remarks>
    /// <example>
    /// <code>
    /// var windowManager = new WindowManager();
    /// // ... use the window manager ...
    /// windowManager.Dispose(); // Clean up resources
    /// </code>
    /// </example>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _isRunning = false;
        _window?.Close();
        if (_windowThread?.IsAlive == true)
        {
            _windowThread.Join(5000);
        }
        _window?.Dispose();
    }
}