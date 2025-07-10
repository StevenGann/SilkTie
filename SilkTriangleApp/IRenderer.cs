using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace SilkTriangleApp;

/// <summary>
/// Defines the contract for renderers that can be managed by the WindowManager.
/// </summary>
/// <remarks>
/// The IRenderer interface provides a standardized way for different types of renderers
/// to integrate with the WindowManager system. This allows for modular rendering
/// where multiple renderers can work together to create complex scenes.
/// 
/// Each renderer is responsible for:
/// - Initializing its own OpenGL resources and shaders
/// - Rendering its content each frame
/// - Handling window resize events
/// - Cleaning up its resources when disposed
/// 
/// The interface extends IDisposable to ensure proper resource cleanup when
/// renderers are no longer needed. This is especially important for OpenGL
/// resources which must be properly disposed to prevent memory leaks.
/// 
/// Common implementations include:
/// - Renderer2D: For 2D graphics and sprites
/// - RendererGUI: For ImGui-based user interfaces
/// - Renderer3D: For 3D models and scenes (future implementation)
/// 
/// Renderers are initialized in the order they are added to the WindowManager
/// and rendered in the same order each frame, allowing for proper layering
/// of different rendering elements.
/// </remarks>
/// <example>
/// <code>
/// // Create a custom renderer
/// public class MyCustomRenderer : IRenderer
/// {
///     private GL? _gl;
///     private uint _shaderProgram;
///     
///     public void Initialize(IWindow window, GL gl)
///     {
///         _gl = gl;
///         // Set up shaders and buffers
///     }
///     
///     public void Render(double deltaTime)
///     {
///         // Render your content
///     }
///     
///     public void OnFramebufferResize(Vector2D&lt;int&gt; size)
///     {
///         // Update viewport or other size-dependent settings
///     }
///     
///     public void Dispose()
///     {
///         // Clean up OpenGL resources
///     }
/// }
/// 
/// // Use with WindowManager
/// var windowManager = new WindowManager();
/// windowManager.AddRenderer(new MyCustomRenderer());
/// </code>
/// </example>
/// <seealso cref="WindowManager"/>
/// <seealso cref="Renderer2D"/>
/// <seealso cref="RendererGUI"/>
public interface IRenderer : IDisposable
{
    /// <summary>
    /// Initializes the renderer with the window and OpenGL context.
    /// </summary>
    /// <param name="window">The window that this renderer is associated with.</param>
    /// <param name="gl">The OpenGL context to use for rendering operations.</param>
    /// <remarks>
    /// This method is called by the WindowManager when the window is first loaded.
    /// It provides the renderer with the necessary window and OpenGL context
    /// references needed for rendering operations.
    /// 
    /// During initialization, the renderer should:
    /// - Store references to the window and GL context
    /// - Create and configure OpenGL resources (buffers, textures, shaders)
    /// - Set up any rendering state or configurations
    /// - Prepare any data structures needed for rendering
    /// 
    /// This method is called only once per renderer instance, typically
    /// after the window has been created but before the first render call.
    /// 
    /// If initialization fails, the renderer should handle the error gracefully
    /// and may choose to disable itself or log the error for debugging.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="window"/> or <paramref name="gl"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// public void Initialize(IWindow window, GL gl)
    /// {
    ///     _window = window ?? throw new ArgumentNullException(nameof(window));
    ///     _gl = gl ?? throw new ArgumentNullException(nameof(gl));
    ///     
    ///     // Create shaders
    ///     _shaderProgram = CreateShaderProgram();
    ///     
    ///     // Set up vertex buffers
    ///     _vao = _gl.GenVertexArray();
    ///     _vbo = _gl.GenBuffer();
    /// }
    /// </code>
    /// </example>
    void Initialize(IWindow window, GL gl);

    /// <summary>
    /// Renders the renderer's content for the current frame.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last frame in seconds.</param>
    /// <remarks>
    /// This method is called by the WindowManager each frame to render the
    /// renderer's content. The delta time parameter can be used for:
    /// - Animation and time-based effects
    /// - Performance monitoring
    /// - Frame rate independent movement
    /// 
    /// During rendering, the renderer should:
    /// - Bind necessary OpenGL resources (shaders, textures, buffers)
    /// - Set up rendering state (blending, depth testing, etc.)
    /// - Issue drawing commands to the GPU
    /// - Clean up any temporary state changes
    /// 
    /// The renderer should be efficient and avoid unnecessary state changes
    /// or resource allocations during rendering to maintain good performance.
    /// 
    /// If the renderer encounters an error during rendering, it should handle
    /// it gracefully without affecting other renderers or the application.
    /// </remarks>
    /// <example>
    /// <code>
    /// public void Render(double deltaTime)
    /// {
    ///     if (_gl == null) return;
    ///     
    ///     // Use shader program
    ///     _gl.UseProgram(_shaderProgram);
    ///     
    ///     // Bind vertex array
    ///     _gl.BindVertexArray(_vao);
    ///     
    ///     // Draw geometry
    ///     _gl.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
    /// }
    /// </code>
    /// </example>
    void Render(double deltaTime);

    /// <summary>
    /// Handles window resize events by updating size-dependent rendering parameters.
    /// </summary>
    /// <param name="size">The new size of the window framebuffer.</param>
    /// <remarks>
    /// This method is called by the WindowManager when the window is resized.
    /// It allows the renderer to update any size-dependent rendering parameters
    /// such as viewports, aspect ratios, or clipping regions.
    /// 
    /// Common updates include:
    /// - Updating the OpenGL viewport (if not handled automatically)
    /// - Recalculating projection matrices
    /// - Adjusting UI element positions and sizes
    /// - Updating render targets or framebuffers
    /// 
    /// The renderer should handle resize events efficiently and avoid
    /// unnecessary resource recreation unless absolutely required.
    /// 
    /// Note that the OpenGL viewport is typically updated automatically
    /// by the window system, so renderers only need to handle additional
    /// size-dependent logic specific to their rendering needs.
    /// </remarks>
    /// <example>
    /// <code>
    /// public void OnFramebufferResize(Vector2D&lt;int&gt; size)
    /// {
    ///     // Update viewport if needed
    ///     _gl?.Viewport(size);
    ///     
    ///     // Update projection matrix for new aspect ratio
    ///     float aspectRatio = (float)size.X / size.Y;
    ///     _projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
    ///         MathHelper.PiOver4, aspectRatio, 0.1f, 100.0f);
    /// }
    /// </code>
    /// </example>
    void OnFramebufferResize(Vector2D<int> size);
} 