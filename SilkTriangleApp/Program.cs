using System.Threading;
using SilkTriangleApp;

class Program
{
    static void Main(string[] args)
    {
        using var windowManager = new WindowManager();
        
        // Add 2D and GUI renderers
        windowManager.AddRenderer(new Renderer2D());
        windowManager.AddRenderer(new RendererGUI());
        
        // Create and run the window (non-blocking)
        windowManager.CreateWindow();
        windowManager.Run();
        
        // Main thread continues immediately
        Console.WriteLine("Window started on separate thread. Main thread continues...");
        Console.WriteLine("Main thread is free to do other work...");
        
        // Simulate some work on the main thread
        for (int i = 0; i < 5; i++)
        {
            Thread.Sleep(1000); // Simulate work
            Console.WriteLine($"Main thread working... ({i + 1}/5)");
        }
        
        Console.WriteLine("Main thread work complete. Waiting for window to close...");
        
        // Wait for the window to close
        windowManager.WaitForWindowToClose();
        
        Console.WriteLine("Window closed. Main thread exiting.");
    }
}
