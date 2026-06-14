namespace TraceType;

// Single-line (Hershey 'futural') skeletons for a-z, in image space (y down).
// char -> strokes; each stroke is a flat [x0,y0,x1,y1,...]. Auto-generated.
public static class Alphabet
{
    public static readonly Dictionary<char, float[][]> Hershey = new()
    {
        ['a'] = new float[][] { new float[]{ 15f, -5f, 15f, 9f }, new float[]{ 15f, -2f, 13f, -4f, 11f, -5f, 8f, -5f, 6f, -4f, 4f, -2f, 3f, 1f, 3f, 3f, 4f, 6f, 6f, 8f, 8f, 9f, 11f, 9f, 13f, 8f, 15f, 6f } },
        ['b'] = new float[][] { new float[]{ 4f, -12f, 4f, 9f }, new float[]{ 4f, -2f, 6f, -4f, 8f, -5f, 11f, -5f, 13f, -4f, 15f, -2f, 16f, 1f, 16f, 3f, 15f, 6f, 13f, 8f, 11f, 9f, 8f, 9f, 6f, 8f, 4f, 6f } },
        ['c'] = new float[][] { new float[]{ 15f, -2f, 13f, -4f, 11f, -5f, 8f, -5f, 6f, -4f, 4f, -2f, 3f, 1f, 3f, 3f, 4f, 6f, 6f, 8f, 8f, 9f, 11f, 9f, 13f, 8f, 15f, 6f } },
        ['d'] = new float[][] { new float[]{ 15f, -12f, 15f, 9f }, new float[]{ 15f, -2f, 13f, -4f, 11f, -5f, 8f, -5f, 6f, -4f, 4f, -2f, 3f, 1f, 3f, 3f, 4f, 6f, 6f, 8f, 8f, 9f, 11f, 9f, 13f, 8f, 15f, 6f } },
        ['e'] = new float[][] { new float[]{ 3f, 1f, 15f, 1f, 15f, -1f, 14f, -3f, 13f, -4f, 11f, -5f, 8f, -5f, 6f, -4f, 4f, -2f, 3f, 1f, 3f, 3f, 4f, 6f, 6f, 8f, 8f, 9f, 11f, 9f, 13f, 8f, 15f, 6f } },
        ['f'] = new float[][] { new float[]{ 10f, -12f, 8f, -12f, 6f, -11f, 5f, -8f, 5f, 9f }, new float[]{ 2f, -5f, 9f, -5f } },
        ['g'] = new float[][] { new float[]{ 15f, -5f, 15f, 11f, 14f, 14f, 13f, 15f, 11f, 16f, 8f, 16f, 6f, 15f }, new float[]{ 15f, -2f, 13f, -4f, 11f, -5f, 8f, -5f, 6f, -4f, 4f, -2f, 3f, 1f, 3f, 3f, 4f, 6f, 6f, 8f, 8f, 9f, 11f, 9f, 13f, 8f, 15f, 6f } },
        ['h'] = new float[][] { new float[]{ 4f, -12f, 4f, 9f }, new float[]{ 4f, -1f, 7f, -4f, 9f, -5f, 12f, -5f, 14f, -4f, 15f, -1f, 15f, 9f } },
        ['i'] = new float[][] { new float[]{ 3f, -12f, 4f, -11f, 5f, -12f, 4f, -13f, 3f, -12f }, new float[]{ 4f, -5f, 4f, 9f } },
        ['j'] = new float[][] { new float[]{ 5f, -12f, 6f, -11f, 7f, -12f, 6f, -13f, 5f, -12f }, new float[]{ 6f, -5f, 6f, 12f, 5f, 15f, 3f, 16f, 1f, 16f } },
        ['k'] = new float[][] { new float[]{ 4f, -12f, 4f, 9f }, new float[]{ 14f, -5f, 4f, 5f }, new float[]{ 8f, 1f, 15f, 9f } },
        ['l'] = new float[][] { new float[]{ 4f, -12f, 4f, 9f } },
        ['m'] = new float[][] { new float[]{ 4f, -5f, 4f, 9f }, new float[]{ 4f, -1f, 7f, -4f, 9f, -5f, 12f, -5f, 14f, -4f, 15f, -1f, 15f, 9f }, new float[]{ 15f, -1f, 18f, -4f, 20f, -5f, 23f, -5f, 25f, -4f, 26f, -1f, 26f, 9f } },
        ['n'] = new float[][] { new float[]{ 4f, -5f, 4f, 9f }, new float[]{ 4f, -1f, 7f, -4f, 9f, -5f, 12f, -5f, 14f, -4f, 15f, -1f, 15f, 9f } },
        ['o'] = new float[][] { new float[]{ 8f, -5f, 6f, -4f, 4f, -2f, 3f, 1f, 3f, 3f, 4f, 6f, 6f, 8f, 8f, 9f, 11f, 9f, 13f, 8f, 15f, 6f, 16f, 3f, 16f, 1f, 15f, -2f, 13f, -4f, 11f, -5f, 8f, -5f } },
        ['p'] = new float[][] { new float[]{ 4f, -5f, 4f, 16f }, new float[]{ 4f, -2f, 6f, -4f, 8f, -5f, 11f, -5f, 13f, -4f, 15f, -2f, 16f, 1f, 16f, 3f, 15f, 6f, 13f, 8f, 11f, 9f, 8f, 9f, 6f, 8f, 4f, 6f } },
        ['q'] = new float[][] { new float[]{ 15f, -5f, 15f, 16f }, new float[]{ 15f, -2f, 13f, -4f, 11f, -5f, 8f, -5f, 6f, -4f, 4f, -2f, 3f, 1f, 3f, 3f, 4f, 6f, 6f, 8f, 8f, 9f, 11f, 9f, 13f, 8f, 15f, 6f } },
        ['r'] = new float[][] { new float[]{ 4f, -5f, 4f, 9f }, new float[]{ 4f, 1f, 5f, -2f, 7f, -4f, 9f, -5f, 12f, -5f } },
        ['s'] = new float[][] { new float[]{ 14f, -2f, 13f, -4f, 10f, -5f, 7f, -5f, 4f, -4f, 3f, -2f, 4f, 0f, 6f, 1f, 11f, 2f, 13f, 3f, 14f, 5f, 14f, 6f, 13f, 8f, 10f, 9f, 7f, 9f, 4f, 8f, 3f, 6f } },
        ['t'] = new float[][] { new float[]{ 5f, -12f, 5f, 5f, 6f, 8f, 8f, 9f, 10f, 9f }, new float[]{ 2f, -5f, 9f, -5f } },
        ['u'] = new float[][] { new float[]{ 4f, -5f, 4f, 5f, 5f, 8f, 7f, 9f, 10f, 9f, 12f, 8f, 15f, 5f }, new float[]{ 15f, -5f, 15f, 9f } },
        ['v'] = new float[][] { new float[]{ 2f, -5f, 8f, 9f }, new float[]{ 14f, -5f, 8f, 9f } },
        ['w'] = new float[][] { new float[]{ 3f, -5f, 7f, 9f }, new float[]{ 11f, -5f, 7f, 9f }, new float[]{ 11f, -5f, 15f, 9f }, new float[]{ 19f, -5f, 15f, 9f } },
        ['x'] = new float[][] { new float[]{ 3f, -5f, 14f, 9f }, new float[]{ 14f, -5f, 3f, 9f } },
        ['y'] = new float[][] { new float[]{ 2f, -5f, 8f, 9f }, new float[]{ 14f, -5f, 8f, 9f, 6f, 13f, 4f, 15f, 2f, 16f, 1f, 16f } },
        ['z'] = new float[][] { new float[]{ 14f, -5f, 3f, 9f }, new float[]{ 3f, -5f, 14f, -5f }, new float[]{ 3f, 9f, 14f, 9f } },
    };
}
