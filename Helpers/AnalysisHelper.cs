namespace ExtractCLUT.Helpers
{
    public static class AnalysisHelper
    {
        public static Dictionary<byte, int>[] GetIndividualHistograms(List<byte[]> listOfByteArrays)
        {
            return listOfByteArrays.Select(array =>
            {
                var histogram = new Dictionary<byte, int>();
                foreach (var value in array)
                {
                    if (!histogram.ContainsKey(value))
                        histogram[value] = 0;
                    histogram[value]++;
                }
                return histogram;
            }).ToArray();
        }

        public static Dictionary<byte, int> GetOverallHistogram(List<byte[]> listOfByteArrays)
        {
            var overallHistogram = new Dictionary<byte, int>();
            foreach (var array in listOfByteArrays)
            {
                foreach (var value in array)
                {
                    if (!overallHistogram.ContainsKey(value))
                        overallHistogram[value] = 0;
                    overallHistogram[value]++;
                }
            }
            return overallHistogram;
        }

        public static void DisplayHistogramInConsole(Dictionary<byte, int> histogram)
        {
            int consoleWidth = 50; // Width of the histogram display
            int maxCount = histogram.Values.Max();

            for (int i = 0; i < 256; i++)
            {
                histogram.TryGetValue((byte)i, out int count);
                int barLength = (int)(count / (double)maxCount * consoleWidth);
                if (barLength == 0) continue;
                Console.WriteLine($"{i,3}: {new string('#', barLength)}");
            }
        }
    }
}
