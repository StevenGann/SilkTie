# Silk.NET Triangle Application - Developer Documentation

## Overview

This is a .NET 9 application that demonstrates a modular rendering architecture using Silk.NET. The application creates a window with both 2D triangle rendering and ImGui-based user interface, running on separate threads for non-blocking operation.

## Project Structure

```
SilkTesting/
├── SilkTriangleApp/          # Main application project
│   ├── Program.cs            # Application entry point
│   ├── IRenderer.cs          # Interface for all renderers
│   ├── Renderer2D.cs         # 2D triangle rendering
│   ├── RendererGUI.cs        # ImGui-based GUI rendering
│   ├── WindowManager.cs      # Window lifecycle and renderer management
│   └── SilkTriangleApp.csproj # Project configuration
├── reference/                # Silk.NET source code reference
│   └── Silk.NET/            # Cloned Silk.NET repository
└── docs/                    # Documentation
    └── notes.md             # This file
```

## Architecture

### Core Components

#### 1. IRenderer Interface (`IRenderer.cs`)
The foundation of the modular rendering system. All renderers must implement this interface:

```csharp
public interface IRenderer : IDisposable
{
    void Initialize(IWindow window, GL gl);
    void Render(double deltaTime);
    void OnFramebufferResize(Vector2D<int> size);
}
```

**Purpose**: Provides a common contract for all rendering components, enabling:
- Multiple renderers in a single window
- Easy addition of new renderer types
- Consistent lifecycle management
- Proper resource cleanup

#### 2. WindowManager (`WindowManager.cs`)
Manages window lifecycle and coordinates multiple renderers:

**Key Features**:
- Non-blocking window operation (runs on separate thread)
- Multiple renderer support via `List<IRenderer>`
- Automatic renderer initialization and cleanup
- Event handling for window lifecycle
- Proper resource disposal

**Public Methods**:
- `CreateWindow()` - Creates and configures the window
- `Run()` - Starts the window on a separate thread
- `AddRenderer(IRenderer)` - Adds a renderer to the system
- `RemoveRenderer(IRenderer)` - Removes a renderer from the system
- `IsRunning` - Property indicating if the window is active
- `WaitForWindowToClose()` - Blocks until window closes

#### 3. Renderer2D (`Renderer2D.cs`)
Handles 2D triangle rendering using OpenGL:

**Features**:
- Renders an orange triangle
- Uses modern OpenGL with VAOs and VBOs
- Custom vertex and fragment shaders
- Proper resource management
- Viewport handling for window resizing

**Rendering Pipeline**:
1. Clear screen with background color
2. Use shader program
3. Bind VAO and draw triangle
4. Clean up resources on disposal

#### 4. RendererGUI (`RendererGUI.cs`)
Handles ImGui-based user interface rendering:

**Features**:
- ImGui demo window display
- Input context management
- Automatic ImGui state updates
- Graceful fallback if ImGui initialization fails
- Proper resource cleanup

**Dependencies**:
- `Silk.NET.OpenGL.Extensions.ImGui`
- `Silk.NET.Input.Glfw`

#### 5. Program (`Program.cs`)
Application entry point demonstrating the architecture:

**Current Setup**:
- Creates WindowManager
- Adds both Renderer2D and RendererGUI
- Demonstrates non-blocking operation
- Shows main thread continuing while window runs

## Dependencies

### NuGet Packages
- `Silk.NET.Core` (2.22.0) - Core Silk.NET functionality
- `Silk.NET.Windowing` (2.22.0) - Window management
- `Silk.NET.OpenGL` (2.22.0) - OpenGL bindings
- `Silk.NET.OpenGL.Extensions.ImGui` (2.22.0) - ImGui integration
- `Silk.NET.Input.Glfw` (2.22.0) - Input handling

### Project Configuration
- Target Framework: .NET 9.0
- AllowUnsafeBlocks: true (required for OpenGL operations)
- ImplicitUsings: enabled
- Nullable: enabled

## Rendering Flow

### Initialization
1. `Program.Main()` creates `WindowManager`
2. Renderers are added via `AddRenderer()`
3. `WindowManager.CreateWindow()` sets up window options
4. `WindowManager.Run()` starts window on separate thread
5. `OnLoad()` event initializes all renderers with window and GL context

### Rendering Loop
1. `OnRender()` event fires each frame
2. Each renderer's `Render()` method is called in order
3. Renderer2D clears screen and draws triangle
4. RendererGUI updates and renders ImGui interface
5. Window swaps buffers

### Window Resizing
1. `OnFramebufferResize()` event fires
2. Each renderer's `OnFramebufferResize()` method is called
3. OpenGL viewport is updated
4. ImGui automatically handles resize

### Cleanup
1. `OnClose()` event fires when window closes
2. Each renderer's `Dispose()` method is called
3. WindowManager waits for thread to finish
4. All resources are properly cleaned up

## Threading Model

### Main Thread
- Creates and configures WindowManager
- Continues execution while window runs
- Can perform other work concurrently
- Waits for window to close before exiting

### Window Thread
- Handles all window events
- Manages OpenGL context
- Executes rendering loop
- Handles input processing

### Benefits
- Non-blocking main thread
- Responsive UI during rendering
- Clean separation of concerns
- Proper resource management

## Extending the System

### Adding a New Renderer
1. Create a new class implementing `IRenderer`
2. Implement required methods:
   - `Initialize()` - Set up OpenGL resources
   - `Render()` - Perform rendering
   - `OnFramebufferResize()` - Handle window resizing
   - `Dispose()` - Clean up resources
3. Add renderer to WindowManager in Program.cs

### Example: Adding Renderer3D
```csharp
public class Renderer3D : IRenderer
{
    // Implementation following IRenderer contract
}

// In Program.cs:
windowManager.AddRenderer(new Renderer3D());
```

### Renderer Order
Renderers are executed in the order they are added:
1. Background/3D renderers first
2. 2D renderers second
3. GUI renderers last (for proper layering)

## Common Issues and Solutions

### Input Platform Error
**Error**: "Couldn't find a suitable input platform for this view"
**Solution**: Ensure `Silk.NET.Input.Glfw` package is installed

### Build Errors with Unsafe Code
**Error**: "Unsafe code may only appear if compiling with /unsafe"
**Solution**: Ensure `AllowUnsafeBlocks` is set to `true` in project file

### Window File Lock
**Error**: "The process cannot access the file because it is being used by another process"
**Solution**: Close any running instances of the application before rebuilding

### ImGui Initialization Failure
**Behavior**: Application continues without ImGui
**Solution**: Check input backend installation and window configuration

## Development Workflow

### Building
```bash
dotnet build
```

### Running
```bash
dotnet run
```

### Debugging
- Set breakpoints in renderer methods
- Use Visual Studio or VS Code debugging
- Monitor console output for initialization messages

### Testing Changes
1. Modify renderer implementation
2. Rebuild project
3. Run application
4. Verify rendering behavior
5. Test window resizing

## Future Enhancements

### Planned Features
- Renderer3D for 3D graphics
- Custom GUI system to replace ImGui
- Particle system renderer
- Post-processing effects
- Renderer priority system

### Architecture Improvements
- Renderer ordering/priority
- Renderer enable/disable functionality
- Performance monitoring
- Configuration system
- Plugin architecture

## Contributing

### Code Style
- Follow C# naming conventions
- Use nullable reference types
- Implement proper resource disposal
- Add XML documentation for public APIs

### Testing
- Test renderer initialization
- Verify resource cleanup
- Test window resizing behavior
- Ensure thread safety

### Documentation
- Update this file when architecture changes
- Document new renderer implementations
- Include usage examples
- Maintain dependency list

## References

- [Silk.NET Documentation](https://github.com/dotnet/Silk.NET)
- [OpenGL Documentation](https://www.opengl.org/documentation/)
- [ImGui Documentation](https://github.com/ocornut/imgui)
- [.NET 9 Documentation](https://docs.microsoft.com/en-us/dotnet/core/introduction) 