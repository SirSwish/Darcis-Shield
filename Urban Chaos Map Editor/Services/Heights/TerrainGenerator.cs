namespace UrbanChaosMapEditor.Services.Heights
{
    public static class TerrainGenerator
    {
        /// <summary>
        /// Generate a 128x128 height field using Diamond-Square, normalized to [-127,127].
        /// roughness ~ 0.4..0.8 (lower = smoother, higher = craggier).
        /// blurPasses 0..2 for a light post-smoothing.
        /// </summary>
        public static sbyte[,] GenerateHeights128(int seed, double roughness = 0.60, int blurPasses = 1)
        {
            // Diamond-square wants (2^n)+1; use 129 then sample to 128.
            const int n = 129;
            var rng = new Random(seed);
            var map = new double[n, n];

            // Init corners
            map[0, 0] = NextRand(rng);
            map[0, n - 1] = NextRand(rng);
            map[n - 1, 0] = NextRand(rng);
            map[n - 1, n - 1] = NextRand(rng);

            int step = n - 1;
            double scale = 1.0;

            while (step > 1)
            {
                int half = step / 2;

                // Diamond step
                for (int y = half; y < n; y += step)
                {
                    for (int x = half; x < n; x += step)
                    {
                        double a = map[x - half, y - half];
                        double b = map[x + half, y - half];
                        double c = map[x - half, y + half];
                        double d = map[x + half, y + half];
                        double avg = 0.25 * (a + b + c + d);
                        map[x, y] = avg + Displace(rng, scale);
                    }
                }

                // Square step
                for (int y = 0; y < n; y += half)
                {
                    int shift = (y / half) % 2 == 0 ? half : 0;
                    for (int x = shift; x < n; x += step)
                    {
                        double sum = 0.0;
                        int cnt = 0;
                        // up
                        if (y - half >= 0) { sum += map[x, y - half]; cnt++; }
                        // down
                        if (y + half < n) { sum += map[x, y + half]; cnt++; }
                        // left
                        if (x - half >= 0) { sum += map[x - half, y]; cnt++; }
                        // right
                        if (x + half < n) { sum += map[x + half, y]; cnt++; }
                        double avg = sum / cnt;
                        map[x, y] = avg + Displace(rng, scale);
                    }
                }

                step /= 2;
                // Reduce displacement scale each octave
                scale *= roughness;
            }

            // Normalize to [-1, 1]
            NormalizeInPlace(map, -1.0, 1.0);

            // Optional very light blur to remove small artifacts
            for (int i = 0; i < blurPasses; i++) BoxBlurInPlace(map);

            // Map to sbyte [-127..127] and sample 128x128 from 129x129
            var outH = new sbyte[128, 128];
            for (int ty = 0; ty < 128; ty++)
            {
                for (int tx = 0; tx < 128; tx++)
                {
                    // Sample center of each cell (or just take same index)
                    double v = map[tx, ty];
                    int scaled = (int)Math.Round(v * 127.0);
                    if (scaled < sbyte.MinValue) scaled = sbyte.MinValue;
                    if (scaled > sbyte.MaxValue) scaled = sbyte.MaxValue;
                    outH[tx, ty] = (sbyte)scaled;
                }
            }

            return outH;
        }

        private static double NextRand(Random rng) => rng.NextDouble() * 2.0 - 1.0;

        private static double Displace(Random rng, double scale) => NextRand(rng) * scale;

        private static void NormalizeInPlace(double[,] map, double newMin, double newMax)
        {
            int w = map.GetLength(0);
            int h = map.GetLength(1);
            double min = double.MaxValue, max = double.MinValue;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double v = map[x, y];
                    if (v < min) min = v;
                    if (v > max) max = v;
                }

            double range = max - min;
            if (range < 1e-9)
            {
                // Degenerate map; flatten
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        map[x, y] = 0.0;
                return;
            }

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double t = (map[x, y] - min) / range; // [0..1]
                    map[x, y] = newMin + t * (newMax - newMin);
                }
        }

        private static void BoxBlurInPlace(double[,] map)
        {
            int w = map.GetLength(0);
            int h = map.GetLength(1);
            var tmp = new double[w, h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double sum = 0.0;
                    int cnt = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= w) continue;
                            sum += map[xx, yy];
                            cnt++;
                        }
                    }
                    tmp[x, y] = sum / cnt;
                }
            }

            Array.Copy(tmp, map, w * h);
        }
    }
}
