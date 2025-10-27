using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Helpers
{
    public static class MathHelpers
    {
        // Greatest Common Divisor (Euclidean Algorithm)
        public static int GCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return Math.Abs(a);
        }

        // Least Common Multiple
        public static int LCM(int a, int b)
        {
            // Handle edge case where a or b is 0 or 1
            if (a == 0 || b == 0) return 0; // Or throw? LCM usually positive.
            if (a == 1) return Math.Abs(b);
            if (b == 1) return Math.Abs(a);

            return Math.Abs(a * b) / GCD(a, b);
        }

        // Helper to calculate LCM for a list of numbers
        public static int CalculateLcmOfList(IEnumerable<int> numbers)
        {
            var positiveNumbers = numbers.Where(n => n > 1).ToList(); // Only consider cycles > 1 step

            if (!positiveNumbers.Any())
            {
                return 1; // If no cycles > 1, only 1 frame needed
            }

            int result = positiveNumbers[0];
            for (int i = 1; i < positiveNumbers.Count; i++)
            {
                result = LCM(result, positiveNumbers[i]);

                // Optional: Add a check for excessively large LCM values if needed
                // if (result > some_reasonable_limit) { return some_reasonable_limit; }
            }
            return result;
        }
    }
}
