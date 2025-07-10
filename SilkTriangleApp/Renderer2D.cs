using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using System.Drawing;
using StbImageSharp;
using System.IO;
using System.Numerics;

namespace SilkTriangleApp;

/// <summary>
/// 2D textured quad renderer that demonstrates texture loading and rendering using modern OpenGL techniques.
/// </summary>
/// <remarks>
/// This renderer creates and renders a textured quad using modern OpenGL features
/// including Vertex Array Objects (VAOs), Vertex Buffer Objects (VBOs), Element Buffer Objects (EBOs),
/// custom shaders, texture loading with alpha blending support, and transformation matrices.
/// 
/// The renderer loads a texture from a PNG file and applies it to a quad with proper
/// texture coordinates and alpha blending. It supports translation, scale, and rotation
/// transformations through uniform matrices passed to the vertex shader.
/// 
/// The quad is rendered in normalized device coordinates (NDC) with vertices at
/// the corners of a square, creating a textured rectangle that can be transformed
/// in real-time.
/// </remarks>
/// <example>
/// <code>
/// // Create and add the 2D renderer to a window manager
/// var windowManager = new WindowManager();
/// var renderer = new Renderer2D();
/// windowManager.AddRenderer(renderer);
/// windowManager.CreateWindow();
/// windowManager.Run();
/// 
/// // Set transformations
/// renderer.SetTranslation(0.5f, 0.0f);
/// renderer.SetScale(2.0f, 2.0f);
/// renderer.SetRotation(45.0f); // degrees
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
    /// operations including buffer creation, shader compilation, texture operations,
    /// and rendering commands.
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
    /// Vertex data for the quad in normalized device coordinates (NDC) with texture coordinates.
    /// </summary>
    /// <remarks>
    /// The quad vertices are defined as:
    /// - Vertex 0: (0.5, 0.5, 0.0) with texture coords (1.0, 1.0) - Top right
    /// - Vertex 1: (0.5, -0.5, 0.0) with texture coords (1.0, 0.0) - Bottom right  
    /// - Vertex 2: (-0.5, -0.5, 0.0) with texture coords (0.0, 0.0) - Bottom left
    /// - Vertex 3: (-0.5, 0.5, 0.0) with texture coords (0.0, 1.0) - Top left
    /// 
    /// Each vertex consists of 5 float values (x, y, z, u, v) where:
    /// - x, y, z are position coordinates in NDC space
    /// - u, v are texture coordinates (0.0 to 1.0 range)
    /// </remarks>
    private static readonly float[] vertices = {
        // Position (x, y, z)    // Texture coords (u, v)
         0.5f,  0.5f, 0.0f,     1.0f, 1.0f,   // Top right
         0.5f, -0.5f, 0.0f,     1.0f, 0.0f,   // Bottom right
        -0.5f, -0.5f, 0.0f,     0.0f, 0.0f,   // Bottom left
        -0.5f,  0.5f, 0.0f,     0.0f, 1.0f    // Top left
    };

    /// <summary>
    /// Index data for the quad using indexed rendering.
    /// </summary>
    /// <remarks>
    /// The indices define two triangles that form a quad:
    /// - Triangle 1: vertices 0, 1, 3 (top right, bottom right, top left)
    /// - Triangle 2: vertices 1, 2, 3 (bottom right, bottom left, top left)
    /// 
    /// Using indexed rendering allows us to reuse vertices and reduces memory usage.
    /// </remarks>
    private static readonly uint[] indices = {
        0, 1, 3,  // First triangle
        1, 2, 3   // Second triangle
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
    /// The VBO contains the actual vertex data (positions and texture coordinates)
    /// that will be sent to the GPU for rendering. This buffer is bound to the VAO
    /// and configured with vertex attribute pointers to define how the data should
    /// be interpreted.
    /// </remarks>
    private uint _vbo;

    /// <summary>
    /// Element Buffer Object (EBO) that stores the index data on the GPU.
    /// </summary>
    /// <remarks>
    /// The EBO contains the index data that defines which vertices to use and in
    /// what order. This allows for efficient rendering by reusing vertices and
    /// reducing the amount of vertex data that needs to be processed.
    /// </remarks>
    private uint _ebo;

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
    /// The OpenGL texture object that stores the loaded texture data.
    /// </summary>
    /// <remarks>
    /// This texture object contains the image data loaded from the PNG file.
    /// It's bound to texture unit 0 and used by the fragment shader to sample
    /// colors for each pixel of the quad.
    /// </remarks>
    private uint _texture;

    /// <summary>
    /// The transformation matrix that combines translation, scale, and rotation.
    /// </summary>
    /// <remarks>
    /// This matrix is calculated by combining the individual transformation matrices
    /// and is updated whenever any transformation parameter changes. It's passed
    /// to the vertex shader as a uniform to transform the quad vertices.
    /// </remarks>
    private Matrix4x4 _transformMatrix = Matrix4x4.Identity;

    /// <summary>
    /// The translation vector for moving the quad.
    /// </summary>
    /// <remarks>
    /// This vector represents the offset in x and y directions. Values are in
    /// normalized device coordinates (NDC) where the screen ranges from -1 to +1
    /// in both dimensions.
    /// </remarks>
    private Vector2 _translation = Vector2.Zero;

    /// <summary>
    /// The scale vector for resizing the quad.
    /// </summary>
    /// <remarks>
    /// This vector represents the scale factors in x and y directions. A value of
    /// 1.0 means no scaling, 2.0 means double size, 0.5 means half size, etc.
    /// </remarks>
    private Vector2 _scale = Vector2.One;

    /// <summary>
    /// The rotation angle in radians.
    /// </summary>
    /// <remarks>
    /// This value represents the rotation around the Z-axis in radians.
    /// Positive values rotate counterclockwise, negative values rotate clockwise.
    /// </remarks>
    private float _rotation = 0.0f;

    /// <summary>
    /// Flag indicating whether the transformation matrix needs to be recalculated.
    /// </summary>
    /// <remarks>
    /// This flag is set to true whenever any transformation parameter changes.
    /// It's checked during rendering to avoid unnecessary matrix recalculations.
    /// </remarks>
    private bool _transformDirty = true;

    /// <summary>
    /// Location of the transform matrix uniform in the shader program.
    /// </summary>
    /// <remarks>
    /// This location is obtained during shader program creation and is used
    /// to update the transformation matrix uniform during rendering.
    /// </remarks>
    private int _transformMatrixLocation;

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
    /// <item><description>Creates and populates the Vertex Buffer Object (VBO) with quad data</description></item>
    /// <item><description>Creates and populates the Element Buffer Object (EBO) with index data</description></item>
    /// <item><description>Sets up vertex attribute pointers for position and texture coordinate data</description></item>
    /// <item><description>Compiles and links vertex and fragment shaders with transformation support</description></item>
    /// <item><description>Creates the shader program</description></item>
    /// <item><description>Loads and configures the texture with proper parameters</description></item>
    /// <item><description>Enables alpha blending for transparency support</description></item>
    /// <item><description>Sets the clear color for the background</description></item>
    /// <item><description>Initializes transformation matrices and uniform locations</description></item>
    /// </list>
    /// 
    /// The initialization process follows modern OpenGL best practices by using
    /// VAOs, VBOs, and EBOs for efficient vertex data management. The texture
    /// is loaded using StbImageSharp and configured with proper filtering and
    /// wrapping modes. The shaders include transformation matrix support for
    /// real-time positioning, scaling, and rotation of the quad.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="window"/> or <paramref name="gl"/> is null.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the texture file "texture.png" is not found in the application directory.
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

        // Create and bind Element Buffer Object (EBO)
        // The EBO will store the index data on the GPU
        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

        // Upload index data to the GPU
        unsafe
        {
            fixed (void* i = &indices[0])
            {
                _gl.BufferData(
                    BufferTargetARB.ElementArrayBuffer,
                    (nuint)(indices.Length * sizeof(uint)),
                    i,
                    BufferUsageARB.StaticDraw
                );
            }
        }

        // Set up vertex attribute pointers
        // This tells OpenGL how to interpret the vertex data
        const uint stride = (3 * sizeof(float)) + (2 * sizeof(float)); // 3 floats for position + 2 floats for texture coords

        // Position attribute (location = 0)
        unsafe
        {
            _gl.VertexAttribPointer(
                0,                              // Attribute location (matches shader)
                3,                              // Number of components per vertex (x, y, z)
                VertexAttribPointerType.Float,  // Data type
                false,                          // Normalized (false for position data)
                stride,                         // Stride (bytes between vertices)
                (void*)0                        // Offset (0 for first attribute)
            );
        }

        // Texture coordinate attribute (location = 1)
        unsafe
        {
            _gl.VertexAttribPointer(
                1,                              // Attribute location (matches shader)
                2,                              // Number of components per vertex (u, v)
                VertexAttribPointerType.Float,  // Data type
                false,                          // Normalized (false for texture coords)
                stride,                         // Stride (bytes between vertices)
                (void*)(3 * sizeof(float))      // Offset (after position data)
            );
        }

        // Enable the vertex attribute arrays
        // This activates the vertex attributes for use in shaders
        _gl.EnableVertexAttribArray(0);
        _gl.EnableVertexAttribArray(1);

        // Create vertex shader source code
        // This shader transforms vertex positions using a transformation matrix and passes texture coordinates
        string vertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec2 aTexCoords;
            
            out vec2 frag_texCoords;
            
            uniform mat4 uTransform;
            
            void main()
            {
                gl_Position = uTransform * vec4(aPosition, 1.0);
                frag_texCoords = aTexCoords;
            }";

        // Create fragment shader source code
        // This shader samples the texture and outputs the color
        string fragmentShaderSource = @"
            #version 330 core
            in vec2 frag_texCoords;
            out vec4 FragColor;
            
            uniform sampler2D uTexture;
            
            void main()
            {
                FragColor = texture(uTexture, frag_texCoords);
            }";

        // Compile vertex shader
        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, vertexShaderSource);
        _gl.CompileShader(vertexShader);

        // Check vertex shader compilation
        _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
        if (vStatus != (int)GLEnum.True)
            throw new Exception("Vertex shader failed to compile: " + _gl.GetShaderInfoLog(vertexShader));

        // Compile fragment shader
        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, fragmentShaderSource);
        _gl.CompileShader(fragmentShader);

        // Check fragment shader compilation
        _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
        if (fStatus != (int)GLEnum.True)
            throw new Exception("Fragment shader failed to compile: " + _gl.GetShaderInfoLog(fragmentShader));

        // Create and link shader program
        // The program combines both shaders for the complete rendering pipeline
        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vertexShader);
        _gl.AttachShader(_shaderProgram, fragmentShader);
        _gl.LinkProgram(_shaderProgram);

        // Check program linking
        _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out int lStatus);
        if (lStatus != (int)GLEnum.True)
            throw new Exception("Program failed to link: " + _gl.GetProgramInfoLog(_shaderProgram));

        // Clean up individual shaders after linking
        // The program retains the compiled shader code, so we can delete the shader objects
        _gl.DetachShader(_shaderProgram, vertexShader);
        _gl.DetachShader(_shaderProgram, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        // Get uniform locations
        _transformMatrixLocation = _gl.GetUniformLocation(_shaderProgram, "uTransform");

        // Create and configure texture
        _texture = _gl.GenTexture();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);

        // Load texture from file
        try
        {
            // Use StbImageSharp to load an image from our PNG file
            ImageResult result = ImageResult.FromMemory(File.ReadAllBytes("texture.png"), ColorComponents.RedGreenBlueAlpha);

            unsafe
            {
                fixed (byte* ptr = result.Data)
                {
                    // Upload texture data to the GPU
                    _gl.TexImage2D(
                        TextureTarget.Texture2D, 
                        0, 
                        InternalFormat.Rgba, 
                        (uint)result.Width, 
                        (uint)result.Height, 
                        0, 
                        PixelFormat.Rgba, 
                        PixelType.UnsignedByte, 
                        ptr
                    );
                }
            }

            // Set texture parameters
            // Wrap mode: repeat the texture when coordinates go outside 0-1 range
            _gl.TextureParameter(_texture, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            _gl.TextureParameter(_texture, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            // Filtering: use bilinear filtering for smooth texture sampling
            _gl.TextureParameter(_texture, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            _gl.TextureParameter(_texture, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            // Generate mipmaps for better quality when texture is scaled down
            _gl.GenerateMipmap(TextureTarget.Texture2D);
        }
        catch (FileNotFoundException)
        {
            // If texture file is not found, create a simple colored texture
            Console.WriteLine("Warning: texture.png not found. Creating a simple colored texture instead.");
            CreateFallbackTexture();
        }

        // Unbind texture as we no longer need to update it
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        // Set texture uniform to use texture unit 0
        int location = _gl.GetUniformLocation(_shaderProgram, "uTexture");
        _gl.Uniform1(location, 0);

        // Enable alpha blending for transparency support
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // Set the clear color for the background
        _gl.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

        // Unbind VAO and buffers as we don't need them bound anymore
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
    }

    /// <summary>
    /// Creates a simple fallback texture when the texture file is not found.
    /// </summary>
    /// <remarks>
    /// This method creates a simple 64x64 texture with a gradient pattern
    /// that can be used as a fallback when the actual texture file is missing.
    /// The texture has alpha transparency to demonstrate alpha blending.
    /// </remarks>
    private void CreateFallbackTexture()
    {
        const int width = 64;
        const int height = 64;
        byte[] textureData = new byte[width * height * 4]; // 4 bytes per pixel (RGBA)

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width + x) * 4;
                
                // Create a simple gradient pattern
                float u = (float)x / width;
                float v = (float)y / height;
                
                // Red component (gradient from left to right)
                textureData[index] = (byte)(u * 255);
                // Green component (gradient from top to bottom)
                textureData[index + 1] = (byte)(v * 255);
                // Blue component (constant)
                textureData[index + 2] = 128;
                // Alpha component (gradient from center)
                float distance = MathF.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
                textureData[index + 3] = (byte)(MathF.Max(0, 1.0f - distance * 2.0f) * 255);
            }
        }

        unsafe
        {
            fixed (byte* ptr = textureData)
            {
                _gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba,
                    width,
                    height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    ptr
                );
            }
        }

        // Set texture parameters
        _gl.TextureParameter(_texture, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        _gl.TextureParameter(_texture, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        _gl.TextureParameter(_texture, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        _gl.TextureParameter(_texture, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.GenerateMipmap(TextureTarget.Texture2D);
    }

    /// <summary>
    /// Sets the translation (position) of the quad.
    /// </summary>
    /// <param name="x">The X translation in normalized device coordinates (-1 to +1).</param>
    /// <param name="y">The Y translation in normalized device coordinates (-1 to +1).</param>
    /// <remarks>
    /// This method sets the position of the quad relative to the center of the screen.
    /// The coordinates are in normalized device coordinates (NDC) where:
    /// - (-1, -1) is the bottom-left corner
    /// - (0, 0) is the center of the screen
    /// - (1, 1) is the top-right corner
    /// 
    /// The translation is applied before scaling and rotation.
    /// </remarks>
    /// <example>
    /// <code>
    /// renderer.SetTranslation(0.5f, 0.0f);  // Move right by half the screen width
    /// renderer.SetTranslation(-0.3f, 0.7f); // Move left and up
    /// </code>
    /// </example>
    public void SetTranslation(float x, float y)
    {
        _translation = new Vector2(x, y);
        _transformDirty = true;
    }

    /// <summary>
    /// Sets the scale (size) of the quad.
    /// </summary>
    /// <param name="x">The X scale factor (1.0 = original size, 2.0 = double size, 0.5 = half size).</param>
    /// <param name="y">The Y scale factor (1.0 = original size, 2.0 = double size, 0.5 = half size).</param>
    /// <remarks>
    /// This method sets the size of the quad relative to its original size.
    /// Scale factors work as follows:
    /// - 1.0: Original size (no scaling)
    /// - 2.0: Double size
    /// - 0.5: Half size
    /// - Negative values: Mirror the quad
    /// 
    /// The scaling is applied after translation but before rotation.
    /// </remarks>
    /// <example>
    /// <code>
    /// renderer.SetScale(2.0f, 2.0f);  // Make the quad twice as large
    /// renderer.SetScale(1.0f, 0.5f);  // Stretch horizontally, shrink vertically
    /// renderer.SetScale(-1.0f, 1.0f); // Mirror horizontally
    /// </code>
    /// </example>
    public void SetScale(float x, float y)
    {
        _scale = new Vector2(x, y);
        _transformDirty = true;
    }

    /// <summary>
    /// Sets the rotation of the quad around its center.
    /// </summary>
    /// <param name="angleDegrees">The rotation angle in degrees (positive = counterclockwise).</param>
    /// <remarks>
    /// This method sets the rotation of the quad around its center point.
    /// The rotation is applied after translation and scaling.
    /// 
    /// Angle values:
    /// - 0°: No rotation
    /// - 90°: Rotate 90 degrees counterclockwise
    /// - 180°: Rotate 180 degrees (upside down)
    /// - -45°: Rotate 45 degrees clockwise
    /// </remarks>
    /// <example>
    /// <code>
    /// renderer.SetRotation(45.0f);   // Rotate 45 degrees counterclockwise
    /// renderer.SetRotation(90.0f);   // Rotate 90 degrees counterclockwise
    /// renderer.SetRotation(-30.0f);  // Rotate 30 degrees clockwise
    /// </code>
    /// </example>
    public void SetRotation(float angleDegrees)
    {
        _rotation = angleDegrees * MathF.PI / 180.0f; // Convert degrees to radians
        _transformDirty = true;
    }

    /// <summary>
    /// Sets all transformations at once for convenience.
    /// </summary>
    /// <param name="translation">The translation vector (x, y).</param>
    /// <param name="scale">The scale vector (x, y).</param>
    /// <param name="rotationDegrees">The rotation angle in degrees.</param>
    /// <remarks>
    /// This method allows setting all transformation parameters in a single call,
    /// which is more efficient than calling the individual setter methods multiple times.
    /// 
    /// The transformations are applied in the order: translation → scale → rotation.
    /// </remarks>
    /// <example>
    /// <code>
    /// renderer.SetTransform(
    ///     new Vector2(0.5f, 0.0f),  // Translation
    ///     new Vector2(2.0f, 2.0f),  // Scale
    ///     45.0f                      // Rotation
    /// );
    /// </code>
    /// </example>
    public void SetTransform(Vector2 translation, Vector2 scale, float rotationDegrees)
    {
        _translation = translation;
        _scale = scale;
        _rotation = rotationDegrees * MathF.PI / 180.0f; // Convert degrees to radians
        _transformDirty = true;
    }

    /// <summary>
    /// Recalculates the transformation matrix from the current transformation parameters.
    /// </summary>
    /// <remarks>
    /// This method combines the translation, scale, and rotation matrices into a single
    /// transformation matrix. The transformations are applied in the order:
    /// translation → scale → rotation.
    /// 
    /// The resulting matrix is used by the vertex shader to transform the quad vertices.
    /// This method is called automatically when the transformation matrix is dirty.
    /// </remarks>
    private void UpdateTransformMatrix()
    {
        if (!_transformDirty) return;

        // Create transformation matrices
        var translationMatrix = Matrix4x4.CreateTranslation(_translation.X, _translation.Y, 0.0f);
        var scaleMatrix = Matrix4x4.CreateScale(_scale.X, _scale.Y, 1.0f);
        var rotationMatrix = Matrix4x4.CreateRotationZ(_rotation);

        // Combine transformations: translation * scale * rotation
        _transformMatrix = translationMatrix * scaleMatrix * rotationMatrix;

        _transformDirty = false;
    }

    /// <summary>
    /// Renders the 2D textured quad for the current frame.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last frame in seconds.</param>
    /// <remarks>
    /// This method performs the following rendering steps:
    /// <list type="number">
    /// <item><description>Checks if OpenGL context is available</description></item>
    /// <item><description>Updates the transformation matrix if needed</description></item>
    /// <item><description>Clears the screen with the background color</description></item>
    /// <item><description>Activates the shader program</description></item>
    /// <item><description>Updates the transformation matrix uniform</description></item>
    /// <item><description>Binds the texture to texture unit 0</description></item>
    /// <item><description>Binds the VAO containing vertex data</description></item>
    /// <item><description>Draws the quad using indexed rendering</description></item>
    /// </list>
    /// 
    /// The rendering process uses the modern OpenGL pipeline with shaders.
    /// The vertex shader transforms the quad vertices using the transformation matrix
    /// and passes texture coordinates, and the fragment shader samples the texture
    /// to color each pixel.
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

        // Update transformation matrix if needed
        UpdateTransformMatrix();

        // Clear the screen with the background color
        // This ensures we start with a clean slate each frame
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        // Activate the shader program
        // This tells OpenGL which shaders to use for rendering
        _gl.UseProgram(_shaderProgram);

        // Update the transformation matrix uniform
        // This passes the transformation matrix to the vertex shader
        unsafe
        {
            fixed (float* matrixPtr = &_transformMatrix.M11)
            {
                _gl.UniformMatrix4(_transformMatrixLocation, 1, false, matrixPtr);
            }
        }

        // Bind the texture to texture unit 0
        // This makes the texture available to the fragment shader
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);

        // Bind the VAO containing our vertex data
        // This activates the vertex attribute configuration we set up during initialization
        _gl.BindVertexArray(_vao);

        // Draw the quad using indexed rendering
        // This sends the rendering command to the GPU
        // Parameters: primitive type, number of indices, index type, offset
        unsafe
        {
            _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
        }
    }

    /// <summary>
    /// Handles window resize events by updating the OpenGL viewport.
    /// </summary>
    /// <param name="size">The new size of the window framebuffer.</param>
    /// <remarks>
    /// This method is called when the window is resized. It updates the OpenGL
    /// viewport to match the new window dimensions, ensuring that the quad
    /// is rendered correctly at the new size.
    /// 
    /// The viewport defines the area of the window where OpenGL will render.
    /// By updating it to match the window size, we ensure that the quad
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
        // This ensures the quad renders correctly at the new dimensions
        _gl?.Viewport(size);
    }

    /// <summary>
    /// Disposes of OpenGL resources and cleans up the renderer.
    /// </summary>
    /// <remarks>
    /// This method performs the following cleanup operations:
    /// <list type="number">
    /// <item><description>Deletes the Vertex Buffer Object (VBO)</description></item>
    /// <item><description>Deletes the Element Buffer Object (EBO)</description></item>
    /// <item><description>Deletes the Vertex Array Object (VAO)</description></item>
    /// <item><description>Deletes the texture object</description></item>
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

        // Delete the Element Buffer Object (EBO)
        // This frees the GPU memory used to store index data
        _gl.DeleteBuffer(_ebo);

        // Delete the Vertex Array Object (VAO)
        // This frees the GPU memory used to store vertex attribute configuration
        _gl.DeleteVertexArray(_vao);

        // Delete the texture object
        // This frees the GPU memory used to store texture data
        _gl.DeleteTexture(_texture);

        // Delete the shader program
        // This frees the GPU memory used to store compiled shaders
        _gl.DeleteProgram(_shaderProgram);

        // Clear references to help with garbage collection
        // This prevents potential issues with disposed OpenGL contexts
        _gl = null;
        _window = null;
    }
}