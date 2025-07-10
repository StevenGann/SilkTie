using System.Threading;
using SilkTriangleApp;

class Program
{
    static void Main(string[] args)
    {
        using var windowManager = new WindowManager();
        
        // Add 2D and GUI renderers
        var renderer2D = new Renderer2D();
        windowManager.AddRenderer(renderer2D);
        windowManager.AddRenderer(new RendererGUI());
        
        // Create and start the window (non-blocking)
        windowManager.CreateWindow();
        windowManager.Run();

        // Wait for the renderer to be initialized and texture to be loaded
        Console.Write("Waiting for renderer to be initialized");
        while (renderer2D.DefaultTextureHandle == 0)
        {
            Thread.Sleep(100);
            Console.Write(".");
        }

        // Now it's safe to create sprites
        Console.WriteLine("\nRenderer initialized, creating sprites...");

        var sprites = new List<Sprite>();
        for (int x = -2; x <= 2; x++)
        {
            for (int y = -2; y <= 2; y++)
            {
                var sprite = new Sprite(
                    new System.Numerics.Vector2(x * 0.3f, y * 0.3f),
                    new System.Numerics.Vector2(0.8f, 0.8f),
                    0.0f,
                    renderer2D.DefaultTextureHandle
                );
                sprites.Add(sprite);
            }
        }
        renderer2D.AddSprites(sprites);
        Console.WriteLine($"Added {sprites.Count} sprites to renderer");



        // Main thread continues immediately
        Console.WriteLine("Window started on separate thread. Main thread continues...");
        Console.WriteLine("Main thread is free to do other work...");

        // Simulate some work on the main thread with animated sprites
        int frameCount = 0;
        while (windowManager.IsRunning)
        {
            Thread.Sleep(1); // Simulate work

            // Update existing sprites with new animations
            float time = frameCount * 0.005f;
            for (int j = 0; j < sprites.Count; j++)
            {
                float offset = j * 0.5f;
                float x = sprites[j].Position.X + MathF.Sin(time + offset) * 0.005f;
                float y = sprites[j].Position.Y + MathF.Cos(time + offset) * 0.005f;
                float scale = 0.8f + MathF.Sin(time * 2 + offset) * 0.2f;
                float rotation = (time * 30.0f + offset * 20.0f) * MathF.PI / 180.0f;
                
                // Update the sprite in place using the new methods
                renderer2D.UpdateSprite(j, 
                    position: new System.Numerics.Vector2(x, y),
                    scale: new System.Numerics.Vector2(scale, scale),
                    rotation: rotation
                );
            }
            
            frameCount++;
            if (frameCount % 100 == 0)
            {
                Console.WriteLine($"Main thread working... Frame {frameCount} - {sprites.Count} sprites animated");
            }
        }
        Console.WriteLine("Window closed. Main thread work complete.");
        windowManager.WaitForWindowToClose();
        Console.WriteLine("Window closed. Main thread exiting.");
    }
}
