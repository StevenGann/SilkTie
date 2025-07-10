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
        Console.WriteLine("Waiting for renderer to be initialized...");
        while (renderer2D.DefaultTextureHandle == 0)
        {
            Thread.Sleep(100);
            Console.Write(".");
        }

        // Now it's safe to create sprites
        Console.WriteLine("Renderer initialized, creating sprites...");

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
        for (int i = 0; i < 1000; i++)
        {
            Thread.Sleep(8); // Simulate work

            // Create new sprites with updated animations
            var updatedSprites = new List<Sprite>();
            float time = i * 0.005f;
            for (int j = 0; j < sprites.Count; j++)
            {
                var sprite = sprites[j];
                float offset = j * 0.5f;
                float x = sprite.Position.X + MathF.Sin(time + offset) * 0.005f;
                float y = sprite.Position.Y + MathF.Cos(time + offset) * 0.005f;
                float scale = 0.8f + MathF.Sin(time * 2 + offset) * 0.2f;
                float rotation = (time * 30.0f + offset * 20.0f) * MathF.PI / 180.0f;
                updatedSprites.Add(new Sprite(
                    new System.Numerics.Vector2(x, y),
                    new System.Numerics.Vector2(scale, scale),
                    rotation,
                    sprite.TextureHandle
                ));
            }
            sprites = updatedSprites;
            renderer2D.ClearSprites();
            renderer2D.AddSprites(sprites);
            if (i % 100 == 0)
            {
                Console.WriteLine($"Main thread working... ({i + 1}/1000) - {sprites.Count} sprites animated");
            }
        }
        Console.WriteLine("Main thread work complete. Waiting for window to close...");
        windowManager.WaitForWindowToClose();
        Console.WriteLine("Window closed. Main thread exiting.");
    }
}
