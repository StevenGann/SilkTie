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
        
        // Create and run the window (non-blocking)
        windowManager.CreateWindow();
        windowManager.Run();
        
        // Main thread continues immediately
        Console.WriteLine("Window started on separate thread. Main thread continues...");
        Console.WriteLine("Main thread is free to do other work...");
        
        // Demonstrate transformations
        Console.WriteLine("Setting up transformations...");
        
        // Set initial transformations
        renderer2D.SetTranslation(0.0f, 0.0f);  // Center of screen
        renderer2D.SetScale(1.5f, 1.5f);        // 1.5x size
        renderer2D.SetRotation(0.0f);            // No rotation
        
        // Simulate some work on the main thread with animated transformations
        for (int i = 0; i < 1000; i++)
        {
            Thread.Sleep(8); // Simulate work
            
            // Animate the quad with different transformations
            float time = i * 0.005f;
            float x = MathF.Sin(time) * 0.3f;  // Oscillate horizontally
            float y = MathF.Cos(time) * 0.2f;  // Oscillate vertically
            float scale = 1.0f + MathF.Sin(time * 2) * 0.3f;  // Pulsing scale
            float rotation = time * 30.0f;  // Continuous rotation
            
            renderer2D.SetTransform(
                new System.Numerics.Vector2(x, y),  // Translation
                new System.Numerics.Vector2(scale, scale),  // Scale
                rotation  // Rotation
            );
            
            Console.WriteLine($"Main thread working... ({i + 1}/10) - Quad transformed");
        }
        
        Console.WriteLine("Main thread work complete. Waiting for window to close...");
        
        // Wait for the window to close
        windowManager.WaitForWindowToClose();
        
        Console.WriteLine("Window closed. Main thread exiting.");
    }
}
