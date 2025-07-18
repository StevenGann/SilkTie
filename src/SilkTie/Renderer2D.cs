using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using System.Drawing;
using StbImageSharp;
using System.IO;
using System.Numerics;

namespace SilkTie;

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
    /// Initializes a new instance of the Renderer2D class with the default texture.
    /// </summary>
    /// <remarks>
    /// This constructor creates a Renderer2D that will attempt to load "texture.png"
    /// from the application directory. If the file is not found, a fallback texture
    /// will be created instead.
    /// </remarks>
    public Renderer2D() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the Renderer2D class with a specified texture file.
    /// </summary>
    /// <param name="texturePath">The path to the texture file to load. If null, uses "texture.png".</param>
    /// <remarks>
    /// This constructor allows specifying a custom texture file path. The texture
    /// will be loaded during initialization. If the file is not found, a fallback
    /// texture will be created instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// var renderer = new Renderer2D("silk.png");
    /// </code>
    /// </example>
        public Renderer2D(string? texturePath)
    {
        _texturePath = texturePath;
    }

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
    /// The compiled and linked shader program used for solid color rendering.
    /// </summary>
    /// <remarks>
    /// This shader program is used for rendering solid color triangles and lines.
    /// It uses a simpler vertex shader and a fragment shader that outputs a solid color.
    /// </remarks>
    private uint _solidColorShaderProgram;

    /// <summary>
    /// Vertex Array Object (VAO) for solid color rendering.
    /// </summary>
    /// <remarks>
    /// This VAO is used for rendering solid color triangles and lines.
    /// It stores the vertex attribute configuration for position-only vertices.
    /// </remarks>
    private uint _solidColorVao;

    /// <summary>
    /// Vertex Buffer Object (VBO) for solid color rendering.
    /// </summary>
    /// <remarks>
    /// This VBO stores vertex data for solid color triangles and lines.
    /// It contains only position data (x, y, z) without texture coordinates.
    /// </remarks>
    private uint _solidColorVbo;

    /// <summary>
    /// Element Buffer Object (EBO) for solid color rendering.
    /// </summary>
    /// <remarks>
    /// This EBO stores index data for solid color triangles and lines.
    /// It allows for efficient rendering by reusing vertices.
    /// </remarks>
    private uint _solidColorEbo;

    /// <summary>
    /// Location of the color uniform in the solid color shader program.
    /// </summary>
    /// <remarks>
    /// This location is obtained during shader program creation and is used
    /// to update the color uniform during rendering.
    /// </remarks>
    private int _colorUniformLocation;

    /// <summary>
    /// Collection of triangles to render.
    /// </summary>
    /// <remarks>
    /// This list stores all triangles that should be rendered each frame.
    /// Each triangle consists of 3 Vector2 points and a color.
    /// Triangles are rendered in the order they appear in this list.
    /// </remarks>
    private readonly List<(Vector2[] points, Vector4 color)> _triangles = new();
    private readonly object _trianglesLock = new object();

    /// <summary>
    /// Flag indicating whether the triangle collection has been modified.
    /// </summary>
    /// <remarks>
    /// This flag is used to optimize rendering by avoiding unnecessary
    /// operations when no triangles have been added or removed.
    /// </remarks>
    private bool _trianglesModified = false;

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
    /// The path to the texture file to load.
    /// </summary>
    /// <remarks>
    /// This path specifies which texture file to load during initialization.
    /// If null or empty, the default "texture.png" will be used.
    /// </remarks>
    private readonly string? _texturePath;

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
    /// Location of the translation uniform in the shader program.
    /// </summary>
    /// <remarks>
    /// This location is obtained during shader program creation and is used
    /// to update the translation uniform during rendering.
    /// </remarks>
    private int _translationLocation;

    /// <summary>
    /// Location of the scale uniform in the shader program.
    /// </summary>
    /// <remarks>
    /// This location is obtained during shader program creation and is used
    /// to update the scale uniform during rendering.
    /// </remarks>
    private int _scaleLocation;

    /// <summary>
    /// Location of the rotation uniform in the shader program.
    /// </summary>
    /// <remarks>
    /// This location is obtained during shader program creation and is used
    /// to update the rotation uniform during rendering.
    /// </remarks>
    private int _rotationLocation;

    /// <summary>
    /// Location of the texture uniform in the shader program.
    /// </summary>
    /// <remarks>
    /// This location is obtained during shader program creation and is used
    /// to update the texture uniform during rendering.
    /// </remarks>
    private int _textureUniformLocation;

    /// <summary>
    /// Collection of sprites to render.
    /// </summary>
    /// <remarks>
    /// This list stores all sprites that should be rendered each frame.
    /// Sprites are rendered in the order they appear in this list.
    /// The list is designed for efficient iteration and modification.
    /// </remarks>
    private readonly List<Sprite> _sprites = new();
    private readonly object _spritesLock = new object();

    /// <summary>
    /// Flag indicating whether the sprite collection has been modified.
    /// </summary>
    /// <remarks>
    /// This flag is used to optimize rendering by avoiding unnecessary
    /// operations when no sprites have been added or removed.
    /// </remarks>
    private bool _spritesModified = false;

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
        // This shader transforms vertex positions using separate translation, rotation, and scale uniforms
        // to ensure rotation happens around the sprite's center
        string vertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec2 aTexCoords;
            
            out vec2 frag_texCoords;
            
            uniform vec2 uTranslation;
            uniform vec2 uScale;
            uniform float uRotation;
            
            void main()
            {
                // Apply transformations in order: translate -> rotate -> scale
                vec2 pos = aPosition.xy;
                
                // Scale first (from center)
                pos *= uScale;
                
                // Rotate around center (0,0)
                float cos_rot = cos(uRotation);
                float sin_rot = sin(uRotation);
                pos = vec2(
                    pos.x * cos_rot - pos.y * sin_rot,
                    pos.x * sin_rot + pos.y * cos_rot
                );
                
                // Translate to final position
                pos += uTranslation;
                
                gl_Position = vec4(pos, 0.0, 1.0);
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
        _translationLocation = _gl.GetUniformLocation(_shaderProgram, "uTranslation");
        _scaleLocation = _gl.GetUniformLocation(_shaderProgram, "uScale");
        _rotationLocation = _gl.GetUniformLocation(_shaderProgram, "uRotation");
        _textureUniformLocation = _gl.GetUniformLocation(_shaderProgram, "uTexture");

        // Create solid color shader program
        CreateSolidColorShaderProgram();

        // Create and configure solid color rendering objects
        CreateSolidColorRenderingObjects();


        // Create and configure texture
        _texture = _gl.GenTexture();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);

        // Load texture from file
        try
        {
            // Use the specified texture path or default to "texture.png"
            string textureFile = string.IsNullOrEmpty(_texturePath) ? "texture.png" : _texturePath;
            
            Console.WriteLine($"Loading texture from: {textureFile}");
            
            // Use StbImageSharp to load an image from our PNG file
            ImageResult result = ImageResult.FromMemory(File.ReadAllBytes(textureFile), ColorComponents.RedGreenBlueAlpha);
            
            Console.WriteLine($"Texture loaded successfully! Size: {result.Width}x{result.Height}, Components: {result.Comp}");

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
            string textureFile = string.IsNullOrEmpty(_texturePath) ? "texture.png" : _texturePath;
            Console.WriteLine($"Warning: {textureFile} not found. Creating a simple colored texture instead.");
            CreateFallbackTexture();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading texture: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            CreateFallbackTexture();
        }


        // Unbind texture as we no longer need to update it
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        // Set texture uniform to use texture unit 0
        _gl.Uniform1(_textureUniformLocation, 0);

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
    /// Creates the solid color shader program for rendering triangles and lines.
    /// </summary>
    /// <remarks>
    /// This method creates a simple shader program that renders solid color triangles.
    /// The vertex shader only handles position data and the fragment shader outputs
    /// a solid color specified by a uniform.
    /// </remarks>
    private void CreateSolidColorShaderProgram()
    {
        // Create vertex shader source code for solid color rendering
        string solidColorVertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            
            void main()
            {
                gl_Position = vec4(aPosition, 1.0);
            }";

        // Create fragment shader source code for solid color rendering
        string solidColorFragmentShaderSource = @"
            #version 330 core
            out vec4 FragColor;
            
            uniform vec4 uColor;
            
            void main()
            {
                FragColor = uColor;
            }";

        // Compile vertex shader
        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, solidColorVertexShaderSource);
        _gl.CompileShader(vertexShader);

        // Check vertex shader compilation
        _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
        if (vStatus != (int)GLEnum.True)
            throw new Exception("Solid color vertex shader failed to compile: " + _gl.GetShaderInfoLog(vertexShader));

        // Compile fragment shader
        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, solidColorFragmentShaderSource);
        _gl.CompileShader(fragmentShader);

        // Check fragment shader compilation
        _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
        if (fStatus != (int)GLEnum.True)
            throw new Exception("Solid color fragment shader failed to compile: " + _gl.GetShaderInfoLog(fragmentShader));

        // Create and link shader program
        _solidColorShaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_solidColorShaderProgram, vertexShader);
        _gl.AttachShader(_solidColorShaderProgram, fragmentShader);
        _gl.LinkProgram(_solidColorShaderProgram);

        // Check program linking
        _gl.GetProgram(_solidColorShaderProgram, ProgramPropertyARB.LinkStatus, out int lStatus);
        if (lStatus != (int)GLEnum.True)
            throw new Exception("Solid color program failed to link: " + _gl.GetProgramInfoLog(_solidColorShaderProgram));

        // Clean up individual shaders after linking
        _gl.DetachShader(_solidColorShaderProgram, vertexShader);
        _gl.DetachShader(_solidColorShaderProgram, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        // Get uniform location for color
        _colorUniformLocation = _gl.GetUniformLocation(_solidColorShaderProgram, "uColor");
    }

    /// <summary>
    /// Creates and configures the rendering objects for solid color triangles.
    /// </summary>
    /// <remarks>
    /// This method creates the VAO, VBO, and EBO for solid color rendering.
    /// These objects are configured to handle position-only vertex data.
    /// </remarks>
    private void CreateSolidColorRenderingObjects()
    {
        // Create and bind Vertex Array Object (VAO) for solid color rendering
        _solidColorVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_solidColorVao);

        // Create and bind Vertex Buffer Object (VBO) for solid color rendering
        _solidColorVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _solidColorVbo);

        // Create and bind Element Buffer Object (EBO) for solid color rendering
        _solidColorEbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _solidColorEbo);

        // Set up vertex attribute pointer for position only
        unsafe
        {
            _gl.VertexAttribPointer(
                0,                              // Attribute location
                3,                              // Number of components per vertex (x, y, z)
                VertexAttribPointerType.Float,  // Data type
                false,                          // Normalized
                3 * sizeof(float),              // Stride (3 floats for position)
                (void*)0                        // Offset
            );
        }

        // Enable the vertex attribute array
        _gl.EnableVertexAttribArray(0);

        // Unbind VAO and buffers
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
    /// Adds a sprite to the renderer.
    /// </summary>
    /// <param name="sprite">The sprite to add.</param>
    /// <remarks>
    /// This method adds a sprite to the internal collection for rendering.
    /// The sprite will be rendered in the next frame and all subsequent frames
    /// until it is removed. Sprites are rendered in the order they were added.
    /// 
    /// This method is thread-safe and can be called from any thread.
    /// </remarks>
    /// <example>
    /// <code>
    /// var sprite = new Sprite(
    ///     new Vector2(0.5f, 0.0f),           // Position
    ///     new Vector2(1.0f, 1.0f),           // Scale
    ///     45.0f * MathF.PI / 180.0f,         // Rotation
    ///     textureId                          // Texture handle
    /// );
    /// renderer.AddSprite(sprite);
    /// </code>
    /// </example>
    public void AddSprite(Sprite sprite)
    {
        lock (_spritesLock)
        {
            _sprites.Add(sprite);
            _spritesModified = true;
        }
    }

    /// <summary>
    /// Adds multiple sprites to the renderer.
    /// </summary>
    /// <param name="sprites">The sprites to add.</param>
    /// <remarks>
    /// This method adds multiple sprites to the internal collection for rendering.
    /// This is more efficient than calling AddSprite multiple times for large
    /// collections of sprites.
    /// 
    /// This method is thread-safe and can be called from any thread.
    /// </remarks>
    /// <example>
    /// <code>
    /// var sprites = new List&lt;Sprite&gt;
    /// {
    ///     Sprite.CreateAt(new Vector2(0.5f, 0.0f), textureId1),
    ///     Sprite.CreateAt(new Vector2(-0.5f, 0.0f), textureId2),
    ///     Sprite.CreateAt(new Vector2(0.0f, 0.5f), textureId3)
    /// };
    /// renderer.AddSprites(sprites);
    /// </code>
    /// </example>
    public void AddSprites(IEnumerable<Sprite> sprites)
    {
        lock (_spritesLock)
        {
            _sprites.AddRange(sprites);
            _spritesModified = true;
        }
    }

    /// <summary>
    /// Removes a sprite from the renderer.
    /// </summary>
    /// <param name="sprite">The sprite to remove.</param>
    /// <returns>True if the sprite was found and removed, false otherwise.</returns>
    /// <remarks>
    /// This method removes a sprite from the internal collection.
    /// The sprite will no longer be rendered in subsequent frames.
    /// 
    /// This method uses reference equality to find the sprite to remove.
    /// If you have multiple sprites with the same properties, you may need
    /// to use RemoveSpriteAt or ClearSprites instead.
    /// 
    /// This method is thread-safe and can be called from any thread.
    /// </remarks>
    /// <example>
    /// <code>
    /// var sprite = Sprite.CreateAt(new Vector2(0.5f, 0.0f), textureId);
    /// renderer.AddSprite(sprite);
    /// // ... later ...
    /// renderer.RemoveSprite(sprite);
    /// </code>
    /// </example>
    public bool RemoveSprite(Sprite sprite)
    {
        lock (_spritesLock)
        {
            var removed = _sprites.Remove(sprite);
            if (removed)
            {
                _spritesModified = true;
            }
            return removed;
        }
    }

    /// <summary>
    /// Removes a sprite at the specified index.
    /// </summary>
    /// <param name="index">The index of the sprite to remove.</param>
    /// <remarks>
    /// This method removes a sprite at the specified index from the internal collection.
    /// The sprite will no longer be rendered in subsequent frames.
    /// 
    /// This method is useful when you know the exact index of the sprite you want
    /// to remove, which can be more efficient than RemoveSprite for large collections.
    /// 
    /// This method is thread-safe and can be called from any thread.
    /// </remarks>
    /// <example>
    /// <code>
    /// renderer.RemoveSpriteAt(0); // Remove the first sprite
    /// </code>
    /// </example>
    public void RemoveSpriteAt(int index)
    {
        lock (_spritesLock)
        {
            if (index >= 0 && index < _sprites.Count)
            {
                _sprites.RemoveAt(index);
                _spritesModified = true;
            }
        }
    }

    /// <summary>
    /// Clears all sprites from the renderer.
    /// </summary>
    /// <remarks>
    /// This method removes all sprites from the internal collection.
    /// No sprites will be rendered in subsequent frames until new ones are added.
    /// 
    /// This method is thread-safe and can be called from any thread.
    /// </remarks>
    /// <example>
    /// <code>
    /// renderer.ClearSprites(); // Remove all sprites
    /// </code>
    /// </example>
    public void ClearSprites()
    {
        lock (_spritesLock)
        {
            _sprites.Clear();
            _spritesModified = true;
        }
    }

    /// <summary>
    /// Updates the position of a sprite at the specified index.
    /// </summary>
    /// <param name="index">The index of the sprite to update.</param>
    /// <param name="position">The new position.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public void UpdateSpritePosition(int index, Vector2 position)
    {
        lock (_spritesLock)
        {
            if (index < 0 || index >= _sprites.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            var sprite = _sprites[index];
            sprite.Position = position;
            _sprites[index] = sprite;
            _spritesModified = true;
        }
    }

    /// <summary>
    /// Updates the scale of a sprite at the specified index.
    /// </summary>
    /// <param name="index">The index of the sprite to update.</param>
    /// <param name="scale">The new scale.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public void UpdateSpriteScale(int index, Vector2 scale)
    {
        lock (_spritesLock)
        {
            if (index < 0 || index >= _sprites.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            var sprite = _sprites[index];
            sprite.Scale = scale;
            _sprites[index] = sprite;
            _spritesModified = true;
        }
    }

    /// <summary>
    /// Updates the rotation of a sprite at the specified index.
    /// </summary>
    /// <param name="index">The index of the sprite to update.</param>
    /// <param name="rotation">The new rotation in radians.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public void UpdateSpriteRotation(int index, float rotation)
    {
        lock (_spritesLock)
        {
            if (index < 0 || index >= _sprites.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            var sprite = _sprites[index];
            sprite.Rotation = rotation;
            _sprites[index] = sprite;
            _spritesModified = true;
        }
    }

    /// <summary>
    /// Updates the texture handle of a sprite at the specified index.
    /// </summary>
    /// <param name="index">The index of the sprite to update.</param>
    /// <param name="textureHandle">The new texture handle.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public void UpdateSpriteTexture(int index, uint textureHandle)
    {
        lock (_spritesLock)
        {
            if (index < 0 || index >= _sprites.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            var sprite = _sprites[index];
            sprite.TextureHandle = textureHandle;
            _sprites[index] = sprite;
            _spritesModified = true;
        }
    }

    /// <summary>
    /// Updates multiple properties of a sprite at the specified index.
    /// </summary>
    /// <param name="index">The index of the sprite to update.</param>
    /// <param name="position">The new position (optional).</param>
    /// <param name="scale">The new scale (optional).</param>
    /// <param name="rotation">The new rotation in radians (optional).</param>
    /// <param name="textureHandle">The new texture handle (optional).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public void UpdateSprite(int index, Vector2? position = null, Vector2? scale = null, float? rotation = null, uint? textureHandle = null)
    {
        lock (_spritesLock)
        {
            if (index < 0 || index >= _sprites.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            var sprite = _sprites[index];
            
            if (position.HasValue)
                sprite.Position = position.Value;
            if (scale.HasValue)
                sprite.Scale = scale.Value;
            if (rotation.HasValue)
                sprite.Rotation = rotation.Value;
            if (textureHandle.HasValue)
                sprite.TextureHandle = textureHandle.Value;
            
            _sprites[index] = sprite;
            _spritesModified = true;
        }
    }

    /// <summary>
    /// Gets a sprite at the specified index.
    /// </summary>
    /// <param name="index">The index of the sprite to access.</param>
    /// <returns>A copy of the sprite at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public Sprite GetSprite(int index)
    {
        lock (_spritesLock)
        {
            if (index < 0 || index >= _sprites.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            return _sprites[index];
        }
    }

    /// <summary>
    /// Gets the number of sprites currently in the renderer.
    /// </summary>
    /// <returns>The number of sprites.</returns>
    /// <remarks>
    /// This property returns the current count of sprites in the internal collection.
    /// This can be useful for debugging or performance monitoring.
    /// </remarks>
    public int SpriteCount 
    { 
        get 
        { 
            lock (_spritesLock) 
            { 
                return _sprites.Count; 
            } 
        } 
    }

    /// <summary>
    /// Gets a read-only view of the sprites in the renderer.
    /// </summary>
    /// <returns>A read-only collection of sprites.</returns>
    /// <remarks>
    /// This property provides a read-only view of the internal sprite collection.
    /// This is useful for debugging or when you need to inspect the sprites
    /// without modifying them.
    /// </remarks>
    public IReadOnlyList<Sprite> Sprites 
    { 
        get 
        { 
            lock (_spritesLock) 
            { 
                return _sprites.AsReadOnly(); 
            } 
        } 
    }

    /// <summary>
    /// Gets the default texture handle used by the renderer.
    /// </summary>
    /// <returns>The OpenGL texture handle.</returns>
    /// <remarks>
    /// This property provides access to the default texture that was loaded
    /// during initialization. This is useful for creating sprites that use
    /// the same texture as the renderer's fallback texture.
    /// </remarks>
    public uint DefaultTextureHandle => _texture;

    /// <summary>
    /// Draws a triangle using the specified points and color.
    /// </summary>
    /// <param name="points">Array of exactly 3 Vector2 points defining the triangle vertices.</param>
    /// <param name="color">The color to fill the triangle with.</param>
    /// <remarks>
    /// This method adds a triangle to the rendering queue. The triangle will be rendered
    /// as a solid color shape using the specified points and color. The points should be
    /// in normalized device coordinates (NDC) where the screen ranges from -1 to +1.
    /// 
    /// The triangle is rendered using the solid color shader program, which is more
    /// efficient than the textured shader for simple shapes.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when points array doesn't contain exactly 3 points.</exception>
    /// <example>
    /// <code>
    /// // Draw a red triangle
    /// renderer.DrawTriangles(
    ///     new Vector2[] { 
    ///         new Vector2(0.0f, 0.5f),   // Top
    ///         new Vector2(-0.5f, -0.5f), // Bottom left
    ///         new Vector2(0.5f, -0.5f)   // Bottom right
    ///     },
    ///     new Vector4(1.0f, 0.0f, 0.0f, 1.0f) // Red
    /// );
    /// </code>
    /// </example>
    public void DrawTriangles(Vector2[] points, Vector4 color)
    {
        if (points == null || points.Length != 3)
            throw new ArgumentException("DrawTriangles requires exactly 3 points.", nameof(points));

        lock (_trianglesLock)
        {
            _triangles.Add((points, color));
            _trianglesModified = true;
        }
    }

    /// <summary>
    /// Draws multiple triangles using the specified points and color.
    /// </summary>
    /// <param name="points">Array of Vector2 points. Must contain a multiple of 3 points.</param>
    /// <param name="color">The color to fill all triangles with.</param>
    /// <remarks>
    /// This method adds multiple triangles to the rendering queue. The points array
    /// should contain a multiple of 3 points, where each group of 3 points defines
    /// one triangle. All triangles will be rendered with the same color.
    /// 
    /// This is more efficient than calling DrawTriangles multiple times for the same color.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when points array length is not a multiple of 3.</exception>
    /// <example>
    /// <code>
    /// // Draw two blue triangles
    /// renderer.DrawTriangles(
    ///     new Vector2[] { 
    ///         // First triangle
    ///         new Vector2(0.0f, 0.5f), new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f),
    ///         // Second triangle
    ///         new Vector2(0.0f, -0.5f), new Vector2(-0.3f, 0.5f), new Vector2(0.3f, 0.5f)
    ///     },
    ///     new Vector4(0.0f, 0.0f, 1.0f, 1.0f) // Blue
    /// );
    /// </code>
    /// </example>
    public void DrawTriangles(Vector2[] points, Vector4 color, int triangleCount)
    {
        if (points == null || points.Length != triangleCount * 3)
            throw new ArgumentException($"DrawTriangles requires exactly {triangleCount * 3} points for {triangleCount} triangles.", nameof(points));

        lock (_trianglesLock)
        {
            for (int i = 0; i < triangleCount; i++)
            {
                var trianglePoints = new Vector2[]
                {
                    points[i * 3],
                    points[i * 3 + 1],
                    points[i * 3 + 2]
                };
                _triangles.Add((trianglePoints, color));
            }
            _trianglesModified = true;
        }
    }

    /// <summary>
    /// Clears all triangles from the renderer.
    /// </summary>
    /// <remarks>
    /// This method removes all triangles from the internal collection.
    /// No triangles will be rendered in subsequent frames until new ones are added.
    /// 
    /// This method is thread-safe and can be called from any thread.
    /// </remarks>
    /// <example>
    /// <code>
    /// renderer.ClearTriangles(); // Remove all triangles
    /// </code>
    /// </example>
    public void ClearTriangles()
    {
        lock (_trianglesLock)
        {
            _triangles.Clear();
            _trianglesModified = true;
        }
    }

    /// <summary>
    /// Gets the number of triangles currently in the renderer.
    /// </summary>
    /// <returns>The number of triangles.</returns>
    /// <remarks>
    /// This property returns the current count of triangles in the internal collection.
    /// This can be useful for debugging or performance monitoring.
    /// </remarks>
    public int TriangleCount 
    { 
        get 
        { 
            lock (_trianglesLock) 
            { 
                return _triangles.Count; 
            } 
        } 
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

        // Combine transformations: translation * rotation * scale
        // This ensures rotation happens around the sprite's center, not the world origin
        _transformMatrix = translationMatrix * rotationMatrix * scaleMatrix;

        _transformDirty = false;
    }

    /// <summary>
    /// Renders all sprites for the current frame.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last frame in seconds.</param>
    /// <remarks>
    /// This method performs the following rendering steps:
    /// <list type="number">
    /// <item><description>Checks if OpenGL context is available</description></item>
    /// <item><description>Clears the screen with the background color</description></item>
    /// <item><description>Activates the shader program</description></item>
    /// <item><description>Binds the VAO containing vertex data</description></item>
    /// <item><description>For each sprite:</description></item>
    /// <item><description>Calculates the transformation matrix</description></item>
    /// <item><description>Updates the transformation matrix uniform</description></item>
    /// <item><description>Binds the sprite's texture</description></item>
    /// <item><description>Draws the quad using indexed rendering</description></item>
    /// </list>
    /// 
    /// The rendering process uses the modern OpenGL pipeline with shaders.
    /// Each sprite is rendered with its own transformation matrix and texture.
    /// The vertex shader transforms the quad vertices using the transformation matrix
    /// and passes texture coordinates, and the fragment shader samples the texture
    /// to color each pixel.
    /// 
    /// If the OpenGL context is not available or no sprites are present,
    /// this method does nothing, allowing the application to continue without errors.
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

        // Get thread-safe copies of the rendering lists
        List<Sprite> spritesToRender;
        List<(Vector2[] points, Vector4 color)> trianglesToRender;
        
        lock (_spritesLock)
        {
            spritesToRender = new List<Sprite>(_sprites);
        }
        
        lock (_trianglesLock)
        {
            trianglesToRender = new List<(Vector2[] points, Vector4 color)>(_triangles);
        }

        // Exit early if nothing to render
        if (spritesToRender.Count == 0 && trianglesToRender.Count == 0)
        {
            return;
        }



        // Clear the screen with the background color
        // This ensures we start with a clean slate each frame
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        // Activate the shader program
        // This tells OpenGL which shaders to use for rendering
        _gl.UseProgram(_shaderProgram);
        


        // Bind the VAO containing our vertex data
        // This activates the vertex attribute configuration we set up during initialization
        _gl.BindVertexArray(_vao);

        // Render each sprite
        foreach (var sprite in spritesToRender)
        {
            // Update the transformation uniforms
            // These are applied in the vertex shader in the correct order
            _gl.Uniform2(_translationLocation, sprite.Position.X, sprite.Position.Y);
            _gl.Uniform2(_scaleLocation, sprite.Scale.X, sprite.Scale.Y);
            _gl.Uniform1(_rotationLocation, sprite.Rotation);

            // Bind the sprite's texture to texture unit 0
            // This makes the texture available to the fragment shader
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, sprite.TextureHandle);
            
            // Update the texture uniform to use texture unit 0
            _gl.Uniform1(_textureUniformLocation, 0);
            

            


            // Draw the quad using indexed rendering
            // This sends the rendering command to the GPU
            // Parameters: primitive type, number of indices, index type, offset
            unsafe
            {
                _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
            }
        }

        // Render triangles using solid color shader
        if (trianglesToRender.Count > 0)
        {
            // Switch to solid color shader program
            _gl.UseProgram(_solidColorShaderProgram);
            
            // Bind the solid color VAO
            _gl.BindVertexArray(_solidColorVao);

            // Render each triangle
            foreach (var (points, color) in trianglesToRender)
            {
                // Convert Vector2 points to float array with Z=0
                float[] vertices = new float[points.Length * 3];
                for (int i = 0; i < points.Length; i++)
                {
                    vertices[i * 3] = points[i].X;     // X
                    vertices[i * 3 + 1] = points[i].Y; // Y
                    vertices[i * 3 + 2] = 0.0f;        // Z
                }

                // Create indices for the triangle (0, 1, 2)
                uint[] indices = { 0, 1, 2 };

                // Upload vertex data
                unsafe
                {
                    fixed (void* v = &vertices[0])
                    {
                        _gl.BufferData(
                            BufferTargetARB.ArrayBuffer,
                            (nuint)(vertices.Length * sizeof(float)),
                            v,
                            BufferUsageARB.DynamicDraw
                        );
                    }
                }

                // Upload index data
                unsafe
                {
                    fixed (void* i = &indices[0])
                    {
                        _gl.BufferData(
                            BufferTargetARB.ElementArrayBuffer,
                            (nuint)(indices.Length * sizeof(uint)),
                            i,
                            BufferUsageARB.DynamicDraw
                        );
                    }
                }

                // Set the color uniform
                _gl.Uniform4(_colorUniformLocation, color.X, color.Y, color.Z, color.W);

                // Draw the triangle
                unsafe
                {
                    _gl.DrawElements(PrimitiveType.Triangles, 3, DrawElementsType.UnsignedInt, (void*)0);
                }
            }
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

        // Delete solid color rendering objects
        _gl.DeleteBuffer(_solidColorVbo);
        _gl.DeleteBuffer(_solidColorEbo);
        _gl.DeleteVertexArray(_solidColorVao);
        _gl.DeleteProgram(_solidColorShaderProgram);

        // Clear references to help with garbage collection
        // This prevents potential issues with disposed OpenGL contexts
        _gl = null;
        _window = null;
    }
}