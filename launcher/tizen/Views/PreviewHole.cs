using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// A see-through hole in Quiet Home's window, the shape of a card. The TV draws an input's
/// live picture on a video plane below the app's window, so the picture shows only where the
/// window is transparent. <see cref="HomeView.OpenHole"/> puts one into the previewed input
/// card; it replaces the pixels drawn before it with transparent ones, the way the underlay
/// hole of DALi's VideoView does. See docs/12-hdmi-preview.md.
/// </summary>
static class PreviewHole
{
    // Positions are the view's own, -0.5..0.5 scaled by its size.
    const string VertexShader = @"
attribute mediump vec2 aPosition;
uniform highp mat4 uMvpMatrix;
uniform highp vec3 uSize;
varying highp vec2 vPosition;
void main()
{
  vPosition = aPosition * uSize.xy;
  gl_Position = uMvpMatrix * vec4(vPosition, 0.0, 1.0);
}";

    // Transparent inside the rounded rectangle, untouched outside its corners.
    const string FragmentShader = @"
uniform highp vec3 uSize;
uniform highp float uRadius;
varying highp vec2 vPosition;
void main()
{
  highp vec2 q = abs(vPosition) - uSize.xy * 0.5 + uRadius;
  if (length(max(q, 0.0)) - uRadius > 0.0)
    discard;
  gl_FragColor = vec4(0.0);
}";

    static Geometry? quad;
    static Shader? shader;

    /// <summary>One corner of the hole's rectangle, matching the shader's <c>aPosition</c> attribute.</summary>
    struct Corner
    {
        public float X, Y;

        /// <summary>Creates the corner at (<paramref name="x"/>, <paramref name="y"/>) in the -0.5..0.5 space of the view.</summary>
        public Corner(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Returns a new hole of <paramref name="w"/> × <paramref name="h"/> with corner radius
    /// <paramref name="radius"/>, to be added as the last child of a card at its top left.
    /// The window must be transparent for the picture below to show through it.
    /// </summary>
    public static View Create(int w, int h, int radius)
    {
        var hole = new View { Size2D = new Size2D(w, h) };
        hole.RegisterProperty("uRadius", new PropertyValue((float)radius));
        // Blending off: the shader's transparent pixels replace the card instead of mixing with it.
        hole.AddRenderer(new Renderer(Quad(), shader ??= new Shader(VertexShader, FragmentShader)) { BlendMode = (int)BlendModeType.Off });
        return hole;
    }

    /// <summary>Returns the two triangles of a rectangle, made once and shared by every hole.</summary>
    static Geometry Quad()
    {
        if (quad != null)
            return quad;
        var format = new PropertyMap();
        format.Add("aPosition", new PropertyValue((int)PropertyType.Vector2));
        var corners = new VertexBuffer(format);
        corners.SetData(new[] { new Corner(-.5f, -.5f), new Corner(.5f, -.5f), new Corner(-.5f, .5f), new Corner(.5f, .5f) });
        quad = new Geometry();
        quad.AddVertexBuffer(corners);
        quad.SetIndexBuffer(new ushort[] { 0, 1, 2, 2, 1, 3 }, 6);
        return quad;
    }
}
