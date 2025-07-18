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
