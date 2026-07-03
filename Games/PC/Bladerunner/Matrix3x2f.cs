namespace BladeRunnerSliceExporter;

// Port of BladeRunner::Matrix3x2 (engines/bladerunner/matrix.h)
// Row-major: _m[2][3]. Represents an affine 2D transform.
public struct Matrix3x2f
{
    public float M00, M01, M02;
    public float M10, M11, M12;

    public Matrix3x2f(
        float m00, float m01, float m02,
        float m10, float m11, float m12)
    {
        M00 = m00; M01 = m01; M02 = m02;
        M10 = m10; M11 = m11; M12 = m12;
    }

    // a * b  (matches operator* in matrix.h, lines 43-54)
    public static Matrix3x2f operator *(Matrix3x2f a, Matrix3x2f b) => new(
        a.M00 * b.M00 + a.M01 * b.M10,
        a.M00 * b.M01 + a.M01 * b.M11,
        a.M00 * b.M02 + a.M01 * b.M12 + a.M02,
        a.M10 * b.M00 + a.M11 * b.M10,
        a.M10 * b.M01 + a.M11 * b.M11,
        a.M10 * b.M02 + a.M11 * b.M12 + a.M12);

    // Accessor mirroring m(r, c)
    public readonly float Get(int r, int c) => (r, c) switch
    {
        (0, 0) => M00,
        (0, 1) => M01,
        (0, 2) => M02,
        (1, 0) => M10,
        (1, 1) => M11,
        (1, 2) => M12,
        _ => throw new IndexOutOfRangeException()
    };
}
