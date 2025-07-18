using System.Numerics;

namespace SilkTie;

/// <summary>
/// Represents a sprite with position, scale, rotation, and texture information.
/// </summary>
/// <remarks>
/// This struct is designed for high-performance rendering of multiple sprites.
/// It uses value semantics and is optimized for cache-friendly iteration over
/// large numbers of sprites. All transformation data is stored inline to
/// minimize memory indirection and improve cache locality.
/// 
/// The struct is intentionally kept small to fit well in CPU cache lines
/// and enable efficient SIMD operations when possible.
/// 
/// Performance characteristics:
/// - Small size (32 bytes) for good cache locality
/// - Value semantics for predictable memory layout
/// - No heap allocations during normal usage
/// - Efficient copying and passing by value
/// </remarks>
/// <example>
/// <code>
/// // Create a sprite
/// var sprite = new Sprite
/// {
///     Position = new Vector2(0.5f, 0.0f),
///     Scale = new Vector2(1.0f, 1.0f),
///     Rotation = 45.0f * MathF.PI / 180.0f, // 45 degrees in radians
///     TextureHandle = textureId
/// };
/// 
/// // Add to renderer
/// renderer.AddSprite(sprite);
/// </code>
/// </example>
public struct Sprite
{
    /// <summary>
    /// The position of the sprite in normalized device coordinates (NDC).
    /// </summary>
    /// <remarks>
    /// This represents the center position of the sprite.
    /// Coordinates range from -1.0 to +1.0 where:
    /// - (-1, -1) is the bottom-left corner of the screen
    /// - (0, 0) is the center of the screen
    /// - (1, 1) is the top-right corner of the screen
    /// </remarks>
    public Vector2 Position;

    /// <summary>
    /// The scale factors for the sprite.
    /// </summary>
    /// <remarks>
    /// This represents the size multipliers for the sprite:
    /// - (1.0, 1.0): Original size
    /// - (2.0, 2.0): Double size
    /// - (0.5, 0.5): Half size
    /// - Negative values: Mirror the sprite
    /// </remarks>
    public Vector2 Scale;

    /// <summary>
    /// The rotation angle in radians.
    /// </summary>
    /// <remarks>
    /// This represents the rotation around the Z-axis:
    /// - 0.0: No rotation
    /// - MathF.PI / 2: 90 degrees counterclockwise
    /// - MathF.PI: 180 degrees (upside down)
    /// - Negative values: Clockwise rotation
    /// </remarks>
    public float Rotation;

    /// <summary>
    /// The OpenGL texture handle (uint) for this sprite.
    /// </summary>
    /// <remarks>
    /// This is the OpenGL texture ID that was returned by glGenTexture().
    /// The texture must be valid and bound to Texture2D target.
    /// A value of 0 indicates no texture (will render as white).
    /// </remarks>
    public uint TextureHandle;

    /// <summary>
    /// Initializes a new sprite with the specified parameters.
    /// </summary>
    /// <param name="position">The position in NDC coordinates.</param>
    /// <param name="scale">The scale factors.</param>
    /// <param name="rotation">The rotation angle in radians.</param>
    /// <param name="textureHandle">The OpenGL texture handle.</param>
    /// <remarks>
    /// This constructor provides a convenient way to create sprites with
    /// all parameters specified at once, avoiding multiple property assignments.
    /// </remarks>
    /// <example>
    /// <code>
    /// var sprite = new Sprite(
    ///     new Vector2(0.5f, 0.0f),           // Position
    ///     new Vector2(1.0f, 1.0f),           // Scale
    ///     45.0f * MathF.PI / 180.0f,         // Rotation (45 degrees)
    ///     textureId                          // Texture handle
    /// );
    /// </code>
    /// </example>
    public Sprite(Vector2 position, Vector2 scale, float rotation, uint textureHandle)
    {
        Position = position;
        Scale = scale;
        Rotation = rotation;
        TextureHandle = textureHandle;
    }

    /// <summary>
    /// Creates a sprite with default values.
    /// </summary>
    /// <param name="textureHandle">The OpenGL texture handle.</param>
    /// <returns>A sprite centered at origin with unit scale and no rotation.</returns>
    /// <remarks>
    /// This static method provides a convenient way to create sprites
    /// with common default values. The sprite will be:
    /// - Centered at (0, 0)
    /// - Unit scale (1.0, 1.0)
    /// - No rotation (0.0)
    /// </remarks>
    /// <example>
    /// <code>
    /// var sprite = Sprite.Create(textureId);
    /// // Equivalent to: new Sprite(Vector2.Zero, Vector2.One, 0.0f, textureId)
    /// </code>
    /// </example>
    public static Sprite Create(uint textureHandle)
    {
        return new Sprite(Vector2.Zero, Vector2.One, 0.0f, textureHandle);
    }

    /// <summary>
    /// Creates a sprite with position only.
    /// </summary>
    /// <param name="position">The position in NDC coordinates.</param>
    /// <param name="textureHandle">The OpenGL texture handle.</param>
    /// <returns>A sprite at the specified position with unit scale and no rotation.</returns>
    /// <remarks>
    /// This is useful for simple sprites that only need positioning.
    /// </remarks>
    /// <example>
    /// <code>
    /// var sprite = Sprite.CreateAt(new Vector2(0.5f, 0.0f), textureId);
    /// </code>
    /// </example>
    public static Sprite CreateAt(Vector2 position, uint textureHandle)
    {
        return new Sprite(position, Vector2.One, 0.0f, textureHandle);
    }

    /// <summary>
    /// Creates a sprite with position and scale.
    /// </summary>
    /// <param name="position">The position in NDC coordinates.</param>
    /// <param name="scale">The scale factors.</param>
    /// <param name="textureHandle">The OpenGL texture handle.</param>
    /// <returns>A sprite at the specified position and scale with no rotation.</returns>
    /// <remarks>
    /// This is useful for sprites that need positioning and scaling but no rotation.
    /// </remarks>
    /// <example>
    /// <code>
    /// var sprite = Sprite.CreateAt(new Vector2(0.5f, 0.0f), new Vector2(2.0f, 2.0f), textureId);
    /// </code>
    /// </example>
    public static Sprite CreateAt(Vector2 position, Vector2 scale, uint textureHandle)
    {
        return new Sprite(position, scale, 0.0f, textureHandle);
    }

    /// <summary>
    /// Returns a string representation of the sprite.
    /// </summary>
    /// <returns>A string containing the sprite's position, scale, rotation, and texture handle.</returns>
    /// <remarks>
    /// This is primarily useful for debugging and logging purposes.
    /// </remarks>
    public override string ToString()
    {
        return $"Sprite(Pos: {Position}, Scale: {Scale}, Rot: {Rotation:F2}rad, Tex: {TextureHandle})";
    }

    /// <summary>
    /// Determines whether this sprite equals another sprite.
    /// </summary>
    /// <param name="other">The sprite to compare with.</param>
    /// <returns>True if all fields are equal, false otherwise.</returns>
    /// <remarks>
    /// This performs a field-by-field comparison of all sprite properties.
    /// </remarks>
    public bool Equals(Sprite other)
    {
        return Position.Equals(other.Position) &&
               Scale.Equals(other.Scale) &&
               Rotation.Equals(other.Rotation) &&
               TextureHandle == other.TextureHandle;
    }

    /// <summary>
    /// Determines whether this sprite equals another object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if obj is a Sprite and all fields are equal, false otherwise.</returns>
    public override bool Equals(object? obj)
    {
        return obj is Sprite other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code for this sprite.
    /// </summary>
    /// <returns>A hash code based on all sprite fields.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Position, Scale, Rotation, TextureHandle);
    }

    /// <summary>
    /// Equality operator for sprites.
    /// </summary>
    /// <param name="left">The left sprite.</param>
    /// <param name="right">The right sprite.</param>
    /// <returns>True if the sprites are equal, false otherwise.</returns>
    public static bool operator ==(Sprite left, Sprite right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator for sprites.
    /// </summary>
    /// <param name="left">The left sprite.</param>
    /// <param name="right">The right sprite.</param>
    /// <returns>True if the sprites are not equal, false otherwise.</returns>
    public static bool operator !=(Sprite left, Sprite right)
    {
        return !left.Equals(right);
    }
} 