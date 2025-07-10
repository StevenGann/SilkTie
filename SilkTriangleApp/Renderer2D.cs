using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using System.Drawing;

namespace SilkTriangleApp;

/// <summary>
/// 2D triangle renderer that demonstrates basic OpenGL rendering using modern OpenGL techniques.
/// </summary>
/// <remarks>
/// This renderer creates and renders a simple orange triangle using modern OpenGL features
/// including Vertex Array Objects (VAOs), Vertex Buffer Objects (VBOs), and custom shaders.
/// It serves as a foundation for 2D graphics rendering in the application.
/// 
/// The renderer is designed to work alongside other renderers in the WindowManager system,
/// providing background or base layer rendering for 2D graphics. It demonstrates proper
/// OpenGL resource management, shader compilation, and rendering pipeline setup.
/// 
/// The triangle is rendered in normalized device coordinates (NDC) with vertices at
/// (-0.5, -0.5), (0.5, -0.5), and (0.0, 0.5), creating an upward-pointing triangle
/// centered in the viewport.
/// </remarks>
/// <example>
/// <code>
/// // Create and add the 2D renderer to a window manager
/// var windowManager = new WindowManager();
/// windowManager.AddRenderer(new Renderer2D());
/// windowManager.CreateWindow();
/// windowManager.Run();
/// </code>
/// </example>
/// <seealso cref="IRenderer"/>
/// <seealso cref="WindowManager"/>
public class Renderer2D : IRenderer
{
    /// <summary>
    /// The OpenGL context used for rendering operations.
    /// </summary>
    /// <remarks>
    /// This GL instance is provided by the WindowManager and is used for all OpenGL
    /// operations including buffer creation, shader compilation, and rendering commands.
    /// </remarks>
    private GL? _gl;

    /// <summary>
    /// The window that this renderer is associated with.
    /// </summary>
    /// <remarks>
    /// This window reference is stored for potential future use, such as getting
    /// window properties or handling window-specific events.
    /// </remarks>
    private IWindow? _window;

    /// <summary>
    /// Vertex data for the triangle in normalized device coordinates (NDC).
    /// </summary>
    /// <remarks>
    /// The triangle vertices are defined as:
    /// - Vertex 0: (-0.5, -0.5, 0.0) - Bottom left
    /// - Vertex 1: (0.5, -0.5, 0.0)  - Bottom right  
    /// - Vertex 2: (0.0, 0.5, 0.0)   - Top center
    /// 
    /// Each vertex consists of 3 float values (x, y, z) where z is set to 0.0
    /// for 2D rendering. The coordinates are in NDC space, which means they
    /// will be automatically transformed to screen coordinates by the GPU.
    /// </remarks>
    private static readonly float[] vertices = {
        -0.5f, -0.5f, 0.0f,  // Left vertex
         0.5f, -0.5f, 0.0f,  // Right vertex
         0.0f,  0.5f, 0.0f   // Top vertex
    };

    /// <summary>
    /// Vertex Array Object (VAO) that stores the vertex attribute configuration.
    /// </summary>
    /// <remarks>
    /// The VAO stores the state of vertex attribute pointers and enables/disables
    /// for vertex attributes. This allows the GPU to efficiently process vertex data
    /// by knowing the layout and format of the vertex attributes.
    /// </remarks>
    private uint _vao;

    /// <summary>
    /// Vertex Buffer Object (VBO) that stores the vertex data on the GPU.
    /// </summary>
    /// <remarks>
    /// The VBO contains the actual vertex data (positions) that will be sent to
    /// the GPU for rendering. This buffer is bound to the VAO and configured
    /// with vertex attribute pointers to define how the data should be interpreted.
    /// </remarks>
    private uint _vbo;

    /// <summary>
    /// The compiled and linked shader program used for rendering.
    /// </summary>
    /// <remarks>
    /// This shader program combines a vertex shader and fragment shader to define
    /// how vertices are processed and how pixels are colored. The program is
    /// created during initialization and used during each render call.
    /// </remarks>
    private uint _shaderProgram;

    /// <summary>
    /// Initializes the 2D renderer with the specified window and OpenGL context.
    /// </summary>
    /// <param name="window">The window to associate with this renderer.</param>
    /// <param name="gl">The OpenGL context to use for rendering.</param>
    /// <remarks>
    /// This method performs the following initialization steps:
    /// <list type="number">
    /// <item><description>Stores references to the window and GL context</description></item>
    /// <item><description>Creates and configures the Vertex Array Object (VAO)</description></item>
    /// <item><description>Creates and populates the Vertex Buffer Object (VBO) with triangle data</description></item>
    /// <item><description>Sets up vertex attribute pointers for position data</description></item>
    /// <item><description>Compiles and links vertex and fragment shaders</description></item>
    /// <item><description>Creates the shader program</description></item>
    /// <item><description>Sets the clear color for the background</description></item>
    /// </list>
    /// 
    /// The initialization process follows modern OpenGL best practices by using
    /// VAOs and VBOs for efficient vertex data management. The shaders are
    /// compiled separately and then linked into a program for use during rendering.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="window"/> or <paramref name="gl"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var renderer = new Renderer2D();
    /// renderer.Initialize(window, gl);
    /// </code>
    /// </example>
    public void Initialize(IWindow window, GL gl)
    {
        // Validate input parameters
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));

        // Create and bind Vertex Array Object (VAO)
        // The VAO stores the vertex attribute configuration
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        // Create and bind Vertex Buffer Object (VBO)
        // The VBO will store the vertex data on the GPU
        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        // Upload vertex data to the GPU
        // Use unsafe block to get pointer to vertex data
        unsafe
        {
            fixed (void* v = &vertices[0])
            {
                _gl.BufferData(
                    BufferTargetARB.ArrayBuffer, 
                    (nuint)(vertices.Length * sizeof(float)), 
                    v, 
                    BufferUsageARB.StaticDraw
                );
            }
        }

        // Set up vertex attribute pointers
        // This tells OpenGL how to interpret the vertex data
        unsafe
        {
            _gl.VertexAttribPointer(
                0,                              // Attribute location (matches shader)
                3,                              // Number of components per vertex (x, y, z)
                VertexAttribPointerType.Float,  // Data type
                false,                          // Normalized (false for position data)
                3 * sizeof(float),              // Stride (bytes between vertices)
                (void*)0                        // Offset (0 for first attribute)
            );
        }

        // Enable the vertex attribute array
        // This activates the vertex attribute for use in shaders
        _gl.EnableVertexAttribArray(0);

        // Create vertex shader source code
        // This shader transforms vertex positions from NDC to clip space
        string vertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec3 aPos;
            void main()
            {
                gl_Position = vec4(aPos.x, aPos.y, aPos.z, 1.0);
            }";

        // Create fragment shader source code
        // This shader sets the color of each pixel to orange
        string fragmentShaderSource = @"
            #version 330 core
            out vec4 FragColor;
            void main()
            {
                FragColor = vec4(1.0f, 0.5f, 0.2f, 1.0f);
            }";

        // Compile vertex shader
        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, vertexShaderSource);
        _gl.CompileShader(vertexShader);

        // Compile fragment shader
        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, fragmentShaderSource);
        _gl.CompileShader(fragmentShader);

        // Create and link shader program
        // The program combines both shaders for the complete rendering pipeline
        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vertexShader);
        _gl.AttachShader(_shaderProgram, fragmentShader);
        _gl.LinkProgram(_shaderProgram);

        // Clean up individual shaders after linking
        // The program retains the compiled shader code, so we can delete the shader objects
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        // Set the clear color for the background
        // This color will be used when clearing the screen each frame
        _gl.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
    }

    /// <summary>
    /// Renders the 2D triangle for the current frame.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last frame in seconds.</param>
    /// <remarks>
    /// This method performs the following rendering steps:
    /// <list type="number">
    /// <item><description>Checks if OpenGL context is available</description></item>
    /// <item><description>Clears the screen with the background color</description></item>
    /// <item><description>Activates the shader program</description></item>
    /// <item><description>Binds the VAO containing vertex data</description></item>
    /// <item><description>Draws the triangle using glDrawArrays</description></item>
    /// </list>
    /// 
    /// The rendering process uses the modern OpenGL pipeline with shaders.
    /// The vertex shader transforms the triangle vertices, and the fragment
    /// shader colors each pixel of the triangle orange.
    /// 
    /// If the OpenGL context is not available, this method does nothing,
    /// allowing the application to continue without errors.
    /// </remarks>
    /// <example>
    /// <code>
    /// // This method is called automatically by the WindowManager each frame
    /// renderer.Render(0.016); // 60 FPS = ~16ms per frame
    /// </code>
    /// </example>
    public void Render(double deltaTime)
    {
        // Check if OpenGL context is available before attempting to render
        if (_gl == null)
        {
            return; // Exit early if OpenGL is not initialized
        }

        // Clear the screen with the background color
        // This ensures we start with a clean slate each frame
        _gl.ClearColor(Color.FromArgb(255, (int)(.45f * 255), (int)(.55f * 255), (int)(.60f * 255)));
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        // Activate the shader program
        // This tells OpenGL which shaders to use for rendering
        _gl.UseProgram(_shaderProgram);

        // Bind the VAO containing our vertex data
        // This activates the vertex attribute configuration we set up during initialization
        _gl.BindVertexArray(_vao);

        // Draw the triangle
        // This sends the rendering command to the GPU
        // Parameters: primitive type, starting vertex, number of vertices
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
    }

    /// <summary>
    /// Handles window resize events by updating the OpenGL viewport.
    /// </summary>
    /// <param name="size">The new size of the window framebuffer.</param>
    /// <remarks>
    /// This method is called when the window is resized. It updates the OpenGL
    /// viewport to match the new window dimensions, ensuring that the triangle
    /// is rendered correctly at the new size.
    /// 
    /// The viewport defines the area of the window where OpenGL will render.
    /// By updating it to match the window size, we ensure that the triangle
    /// scales properly with the window and maintains its aspect ratio.
    /// </remarks>
    /// <example>
    /// <code>
    /// // This method is called automatically by the WindowManager on resize
    /// renderer.OnFramebufferResize(new Vector2D&lt;int&gt;(1920, 1080));
    /// </code>
    /// </example>
    public void OnFramebufferResize(Vector2D<int> size)
    {
        // Update the OpenGL viewport to match the new window size
        // This ensures the triangle renders correctly at the new dimensions
        _gl?.Viewport(size);
    }

    /// <summary>
    /// Disposes of OpenGL resources and cleans up the renderer.
    /// </summary>
    /// <remarks>
    /// This method performs the following cleanup operations:
    /// <list type="number">
    /// <item><description>Deletes the Vertex Buffer Object (VBO)</description></item>
    /// <item><description>Deletes the Vertex Array Object (VAO)</description></item>
    /// <item><description>Deletes the shader program</description></item>
    /// <item><description>Clears references to prevent memory leaks</description></item>
    /// </list>
    /// 
    /// This method is called automatically by the WindowManager when the window
    /// is closing or when the renderer is being removed from the system.
    /// 
    /// It's important to call this method to prevent OpenGL resource leaks,
    /// especially when the application is shutting down or when switching
    /// between different renderer configurations.
    /// </remarks>
    /// <example>
    /// <code>
    /// // This method is called automatically by the WindowManager
    /// renderer.Dispose();
    /// </code>
    /// </example>
    public void Dispose()
    {
        // Check if OpenGL context is available before attempting cleanup
        if (_gl == null)
        {
            return; // Exit early if OpenGL is not initialized
        }

        // Delete the Vertex Buffer Object (VBO)
        // This frees the GPU memory used to store vertex data
        _gl.DeleteBuffer(_vbo);

        // Delete the Vertex Array Object (VAO)
        // This frees the GPU memory used to store vertex attribute configuration
        _gl.DeleteVertexArray(_vao);

        // Delete the shader program
        // This frees the GPU memory used to store compiled shaders
        _gl.DeleteProgram(_shaderProgram);

        // Clear references to help with garbage collection
        // This prevents potential issues with disposed OpenGL contexts
        _gl = null;
        _window = null;
    }
}