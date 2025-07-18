using SilkTie;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using System.Numerics;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("SilkTie Simple Sprite Demo");
        Console.WriteLine("Press ESC to exit");
        Console.WriteLine();

        try
        {
            // Create window manager and renderer
            Console.WriteLine("Creating WindowManager...");
            var windowManager = new WindowManager();
            
            Console.WriteLine("Creating Renderer2D...");
            var renderer = new Renderer2D("silk.png");
            
            Console.WriteLine("Adding renderer to window manager...");
            windowManager.AddRenderer(renderer);
            
            Console.WriteLine("Creating window...");
            windowManager.CreateWindow("SilkTie Simple Demo", 800, 600);
            
            Console.WriteLine("Window created successfully!");

            // Set up window load callback
            windowManager.OnWindowLoad += () =>
            {
                Console.WriteLine("Window loaded and ready!");
                
                // Create sprite after initialization
                Console.WriteLine("Creating sprite after initialization...");
                var sprite = Sprite.CreateAt(new Vector2(0.0f, 0.0f), renderer.DefaultTextureHandle);
                renderer.AddSprite(sprite);
                
                Console.WriteLine($"Sprite created after init! Texture handle: {renderer.DefaultTextureHandle}");
                Console.WriteLine($"Sprite texture handle: {sprite.TextureHandle}");
                Console.WriteLine($"Sprite position: {sprite.Position}");
                Console.WriteLine($"Sprite scale: {sprite.Scale}");
                Console.WriteLine($"Sprite rotation: {sprite.Rotation}");

                // Draw some solid triangles
                Console.WriteLine("Drawing solid triangles...");
                
                // Draw a red triangle on the left
                Vector2[] redTriangle = new Vector2[]
                {
                    new Vector2(-0.7f, 0.0f),   // Top
                    new Vector2(-0.9f, -0.3f),  // Bottom left
                    new Vector2(-0.5f, -0.3f)   // Bottom right
                };
                renderer.DrawTriangles(redTriangle, new Vector4(1.0f, 0.0f, 0.0f, 1.0f)); // Red
                
                // Draw a green triangle on the right
                Vector2[] greenTriangle = new Vector2[]
                {
                    new Vector2(0.7f, 0.0f),    // Top
                    new Vector2(0.5f, -0.3f),   // Bottom left
                    new Vector2(0.9f, -0.3f)    // Bottom right
                };
                renderer.DrawTriangles(greenTriangle, new Vector4(0.0f, 1.0f, 0.0f, 1.0f)); // Green
                
                // Draw a blue triangle at the top
                Vector2[] blueTriangle = new Vector2[]
                {
                    new Vector2(0.0f, 0.7f),    // Top
                    new Vector2(-0.2f, 0.4f),   // Bottom left
                    new Vector2(0.2f, 0.4f)     // Bottom right
                };
                renderer.DrawTriangles(blueTriangle, new Vector4(0.0f, 0.0f, 1.0f, 1.0f)); // Blue
                
                // Draw a yellow triangle at the bottom
                Vector2[] yellowTriangle = new Vector2[]
                {
                    new Vector2(0.0f, -0.4f),   // Top
                    new Vector2(-0.2f, -0.7f),  // Bottom left
                    new Vector2(0.2f, -0.7f)    // Bottom right
                };
                renderer.DrawTriangles(yellowTriangle, new Vector4(1.0f, 1.0f, 0.0f, 1.0f)); // Yellow
                
                Console.WriteLine("Triangles drawn successfully!");
            };

            // Set up window close callback
            windowManager.OnWindowClose += () =>
            {
                Console.WriteLine("Window closing...");
            };

            Console.WriteLine("Starting window...");
            Console.WriteLine("Window should appear now. Press ESC to exit.");
            
            // Run the application (non-blocking)
            windowManager.Run();
            
            // Wait for the window to close
            windowManager.WaitForWindowToClose();
            
            Console.WriteLine("Window closed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
