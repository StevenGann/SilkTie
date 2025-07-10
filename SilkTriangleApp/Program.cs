using System.Threading;
using System.Numerics;
using SilkTriangleApp;

class Program
{
    // Benchmark configuration
    private const int MaxSprites = int.MaxValue;
    private const int SpriteIncrement = 100;
    private const int TargetFPS = 60;
    
    // Bunny physics record
    private record struct Bunny
    {
        public Vector2 Position;
        public Vector2 Speed;
        public Vector2 Scale;
        public float Rotation;
        public uint TextureHandle;
        
        public Bunny(Vector2 position, Vector2 speed, Vector2 scale, uint textureHandle)
        {
            Position = position;
            Speed = speed;
            Scale = scale;
            Rotation = 0.0f;
            TextureHandle = textureHandle;
        }
    }

    static void Main(string[] args)
    {
        using var windowManager = new WindowManager();
        
        // Add 2D and GUI renderers
        var renderer2D = new Renderer2D();
        windowManager.AddRenderer(renderer2D);
        windowManager.AddRenderer(new RendererGUI());
        
        // Create and start the window (non-blocking)
        windowManager.CreateWindow("Silk.NET Bunnymark Benchmark", 800, 600);
        windowManager.Run();

        // Wait for the renderer to be initialized and texture to be loaded
        Console.Write("Waiting for renderer to be initialized");
        while (renderer2D.DefaultTextureHandle == 0)
        {
            Thread.Sleep(100);
            Console.Write(".");
        }

        // Now it's safe to create sprites
        Console.WriteLine("\nRenderer initialized, creating bunnies...");

        // Initialize bunny storage
        var bunnies = new Bunny[MaxSprites];
        int bunnyCount = 0;
        
        // Add initial batch of bunnies
        AddBunnies(ref bunnyCount, bunnies, SpriteIncrement, renderer2D.DefaultTextureHandle);
        
        // Convert bunnies to sprites and add to renderer
        var sprites = new List<Sprite>();
        for (int i = 0; i < bunnyCount; i++)
        {
            sprites.Add(new Sprite(
                bunnies[i].Position,
                bunnies[i].Scale,
                bunnies[i].Rotation,
                bunnies[i].TextureHandle
            ));
        }
        renderer2D.AddSprites(sprites);
        
        Console.WriteLine($"Added {bunnyCount} bunnies to renderer");
        Console.WriteLine("Window started on separate thread. Main thread continues...");
        Console.WriteLine("Main thread is free to do other work...");

        // Main simulation loop
        int frameCount = 0;
        while (windowManager.IsRunning)
        {
            // Update bunny physics
            UpdateBunnies(bunnies, bunnyCount, 800, 600);
            
            // Update sprites with new bunny data
            for (int i = 0; i < bunnyCount; i++)
            {
                renderer2D.UpdateSprite(i, 
                    position: bunnies[i].Position,
                    scale: bunnies[i].Scale,
                    rotation: bunnies[i].Rotation
                );
            }
            
            frameCount++;
            if (frameCount % 60 == 0) // Log every 60 frames (roughly once per second)
            {
                Console.WriteLine($"Frame {frameCount} - {bunnyCount} bunnies - FPS: {windowManager.CurrentFPS:F1} - Avg FPS: {windowManager.AverageFPS:F1}");
            }
            
            Thread.Sleep(1);
        }
        
        Console.WriteLine("Window closed. Main thread work complete.");
        windowManager.WaitForWindowToClose();
        Console.WriteLine("Window closed. Main thread exiting.");
    }
    
    private static void AddBunnies(ref int bunnyCount, Bunny[] bunnies, int count, uint textureHandle)
    {
        var random = new Random();
        for (int i = 0; i < count && bunnyCount < MaxSprites; i++)
        {
            // Random position within window bounds (convert to NDC coordinates)
            var position = new Vector2(
                (random.Next(0, 800) / 400.0f) - 1.0f,  // Convert 0-800 to -1 to +1
                (random.Next(0, 600) / 300.0f) - 1.0f   // Convert 0-600 to -1 to +1
            );
            
            // Random speed (scaled for target FPS and NDC coordinates)
            var speed = new Vector2(
                (random.Next(-250, 250) / (float)TargetFPS) / 400.0f,  // Scale for NDC
                (random.Next(-250, 250) / (float)TargetFPS) / 300.0f   // Scale for NDC
            );

            var scale = new Vector2(
                (random.Next(10, 20) / 200.0f),
                (random.Next(10, 20) / 200.0f)
            );
            
            bunnies[bunnyCount] = new Bunny(position, speed, scale, textureHandle);
            bunnyCount++;
        }
    }
    
    private static void UpdateBunnies(Bunny[] bunnies, int count, int screenWidth, int screenHeight)
    {
        for (int i = 0; i < count; i++)
        {
            ref var bunny = ref bunnies[i];
            
            // Integrate position
            bunny.Position += bunny.Speed * 0.1f;
            
            // Bounce off screen borders (NDC coordinates)
            if (bunny.Position.X <= -1.0f || bunny.Position.X >= 1.0f)
            {
                bunny.Speed.X *= -1;
            }
            if (bunny.Position.Y <= -1.0f || bunny.Position.Y >= 1.0f)
            {
                bunny.Speed.Y *= -1;
            }
            
            // Keep bunnies within bounds (NDC coordinates)
            //bunny.Position.X = Math.Clamp(bunny.Position.X, -1.0f, 1.0f);
            //bunny.Position.Y = Math.Clamp(bunny.Position.Y, -1.0f, 1.0f);
            
            // Add some rotation for visual interest
            bunny.Rotation += 0.02f;
        }
    }
}
