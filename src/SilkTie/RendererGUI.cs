using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Input;
using System.Drawing;
using Silk.NET.Maths;

namespace SilkTie;

/// <summary>
/// ImGui-based GUI renderer that provides immediate mode user interface rendering.
/// </summary>
/// <remarks>
/// This renderer integrates Dear ImGui with Silk.NET to provide a lightweight,
/// immediate mode GUI system. It handles ImGui initialization, input processing,
/// and rendering of GUI elements such as windows, buttons, sliders, and other widgets.
/// 
/// The renderer is designed to be added to a WindowManager alongside other renderers
/// (like Renderer2D or Renderer3D) to provide overlay GUI functionality.
/// 
/// If ImGui initialization fails (e.g., due to missing input backend), the renderer
/// will continue to function without GUI capabilities, allowing the application
/// to run with other renderers intact.
/// </remarks>
/// <example>
/// <code>
/// // Create and add the GUI renderer to a window manager
/// var windowManager = new WindowManager();
/// windowManager.AddRenderer(new RendererGUI());
/// windowManager.CreateWindow();
/// windowManager.Run();
/// </code>
/// </example>
/// <seealso cref="IRenderer"/>
/// <seealso cref="WindowManager"/>
public class RendererGUI : IRenderer
{
    /// <summary>
    /// The ImGui controller that manages the ImGui context and rendering.
    /// </summary>
    /// <remarks>
    /// This controller handles the integration between ImGui and Silk.NET,
    /// including input processing, rendering state management, and resource cleanup.
    /// </remarks>
    private ImGuiController? _controller;

    /// <summary>
    /// The input context that provides mouse and keyboard input to ImGui.
    /// </summary>
    /// <remarks>
    /// This context is created from the window and provides the necessary input
    /// events that ImGui needs to function properly. It handles mouse movement,
    /// clicks, keyboard input, and other user interactions.
    /// </remarks>
    private IInputContext? _inputContext;

    /// <summary>
    /// The OpenGL context used for rendering ImGui elements.
    /// </summary>
    /// <remarks>
    /// This GL instance is provided by the WindowManager and is used by the
    /// ImGui controller to render GUI elements using OpenGL commands.
    /// </remarks>
    private GL? _gl;

    /// <summary>
    /// The window that this renderer is associated with.
    /// </summary>
    /// <remarks>
    /// This window reference is used to create the input context and is passed
    /// to the ImGui controller for proper integration.
    /// </remarks>
    private IWindow? _window;

    /// <summary>
    /// Initializes the ImGui renderer with the specified window and OpenGL context.
    /// </summary>
    /// <param name="window">The window to associate with this renderer.</param>
    /// <param name="gl">The OpenGL context to use for rendering.</param>
    /// <remarks>
    /// This method performs the following initialization steps:
    /// <list type="number">
    /// <item><description>Stores references to the window and GL context</description></item>
    /// <item><description>Creates an input context from the window</description></item>
    /// <item><description>Initializes the ImGui controller with the window, GL context, and input context</description></item>
    /// <item><description>Handles any initialization errors gracefully</description></item>
    /// </list>
    /// 
    /// If ImGui initialization fails (e.g., due to missing input backend packages),
    /// the error is logged to the console and the renderer continues without GUI
    /// capabilities. This ensures that the application can still run with other
    /// renderers even if ImGui is not available.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="window"/> or <paramref name="gl"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var renderer = new RendererGUI();
    /// renderer.Initialize(window, gl);
    /// </code>
    /// </example>
    public void Initialize(IWindow window, GL gl)
    {
        // Validate input parameters
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));

        try
        {
            // Create input context from the window
            // This provides mouse and keyboard input to ImGui
            _inputContext = _window.CreateInput();

            // Initialize ImGui controller with the window, GL context, and input context
            // This sets up the integration between ImGui and Silk.NET
            _controller = new ImGuiController(_gl, _window, _inputContext);
        }
        catch (Exception ex)
        {
            // Log the error but continue without ImGui
            // This allows the application to run with other renderers
            Console.WriteLine($"Failed to initialize ImGui: {ex.Message}");
            Console.WriteLine("Application will continue without GUI functionality.");
        }
    }

    /// <summary>
    /// Renders the ImGui interface for the current frame.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last frame in seconds.</param>
    /// <remarks>
    /// This method performs the following rendering steps:
    /// <list type="number">
    /// <item><description>Updates the ImGui state with the current delta time</description></item>
    /// <item><description>Renders the ImGui demo window (if ImGui is available)</description></item>
    /// <item><description>Renders all ImGui elements to the screen</description></item>
    /// </list>
    /// 
    /// If ImGui is not available (due to initialization failure), this method
    /// does nothing, allowing other renderers to continue functioning normally.
    /// 
    /// The ImGui demo window provides examples of various GUI widgets and can be
    /// useful for testing and development. In a production application, you would
    /// typically replace this with your own custom GUI elements.
    /// </remarks>
    /// <example>
    /// <code>
    /// // This method is called automatically by the WindowManager each frame
    /// renderer.Render(0.016); // 60 FPS = ~16ms per frame
    /// </code>
    /// </example>
    public void Render(double deltaTime)
    {
        // Check if ImGui is available before attempting to render
        if (_controller == null)
        {
            return; // Exit early if ImGui is not initialized
        }

        // Update ImGui state with the current delta time
        // This processes input events and updates internal state
        _controller.Update((float)deltaTime);

        // Render the ImGui demo window
        // This shows various ImGui widgets and can be useful for testing
        // In production, you would replace this with your own GUI elements
        ImGuiNET.ImGui.ShowDemoWindow();

        // Render all ImGui elements to the screen
        // This draws the GUI overlay on top of other rendered content
        _controller.Render();
    }

    /// <summary>
    /// Handles window resize events for ImGui.
    /// </summary>
    /// <param name="size">The new size of the window framebuffer.</param>
    /// <remarks>
    /// This method is called when the window is resized. ImGui automatically
    /// handles viewport and projection matrix updates through its controller,
    /// so this method currently does not require any additional implementation.
    /// 
    /// The ImGui controller maintains its own state and automatically adjusts
    /// to window size changes during the rendering process.
    /// </remarks>
    /// <example>
    /// <code>
    /// // This method is called automatically by the WindowManager on resize
    /// renderer.OnFramebufferResize(new Vector2D&lt;int&gt;(1920, 1080));
    /// </code>
    /// </example>
    public void OnFramebufferResize(Vector2D<int> size)
    {
        // ImGui automatically handles resize events through its controller
        // The controller updates viewport and projection matrices as needed
        // during the rendering process, so no additional work is required here
    }

    /// <summary>
    /// Disposes of ImGui resources and cleans up the renderer.
    /// </summary>
    /// <remarks>
    /// This method performs the following cleanup operations:
    /// <list type="number">
    /// <item><description>Disposes of the ImGui controller and its associated resources</description></item>
    /// <item><description>Disposes of the input context</description></item>
    /// <item><description>Clears references to prevent memory leaks</description></item>
    /// </list>
    /// 
    /// This method is called automatically by the WindowManager when the window
    /// is closing or when the renderer is being removed from the system.
    /// 
    /// It's important to call this method to prevent resource leaks, especially
    /// when the application is shutting down or when switching between different
    /// renderer configurations.
    /// </remarks>
    /// <example>
    /// <code>
    /// // This method is called automatically by the WindowManager
    /// renderer.Dispose();
    /// </code>
    /// </example>
    public void Dispose()
    {
        // Dispose of ImGui controller and its associated resources
        // This includes shaders, textures, and other OpenGL resources
        _controller?.Dispose();

        // Dispose of the input context
        // This releases any system resources associated with input handling
        _inputContext?.Dispose();

        // Clear references to help with garbage collection
        _controller = null;
        _inputContext = null;
        _gl = null;
        _window = null;
    }
} 