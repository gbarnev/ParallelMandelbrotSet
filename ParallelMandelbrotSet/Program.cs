using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelMandelbrotSet
{
    class Program
    {
        class MandelbrotScale
        {
            public MandelbrotScale(double lx, double hx, double ly, double hy)
            {
                Lx = lx;
                Hx = hx;
                Ly = ly;
                Hy = hy;
            }

            public double Lx { get; set; }
            public double Hx { get; set; }
            public double Ly { get; set; }
            public double Hy { get; set; }
        }

        const int maxIterations = 1000;
        static int width = 640;
        static int height = 480;
        static bool IsQuiet = false;
        static bool IsTest = false;
        static int granularity = 2;
        static int taskCnt = 1;
        static string path = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + '\\' + "zad18.png";
        static MandelbrotScale scale = new MandelbrotScale(-2.2, 2.2, -2.2, 2.2);

        static void Main(string[] args)
        {
            ParseArgs(args);

            if(IsTest)
            {
                PerformTest(taskCnt);
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
                return;
            }

            var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            if(taskCnt == 1)
            {
                Time(() =>
                    DrawMandelbrotSet(width, height, fileStream, scale),
                    nameof(DrawMandelbrotSet),
                    0, 1);
            }
            else
            {
                Time(() =>
                DrawMandelbrotSetParallel(width, height, fileStream, taskCnt, granularity, scale),
                nameof(DrawMandelbrotSetParallel),
                granularity,
                taskCnt);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
        }

        static void PerformTest(int testUntil)
        {
            var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

            Console.WriteLine("P      | Tp      | Sp      | Ep     ");
            Console.WriteLine("---------------------------------------");

            var sw = Stopwatch.StartNew();
            DrawMandelbrotSet(width, height, fileStream, scale);
            double t1 = sw.ElapsedMilliseconds; //serial
            Console.WriteLine($"{1,-5} | {t1,-7} | {1,-7: ##.####} | {1,-7: ##.####}");
 
            for(int p = 2; p <= testUntil; p+=2)
            {
                sw.Restart();
                DrawMandelbrotSetParallel(width, height, fileStream, p, granularity, scale);
                double tp = sw.ElapsedMilliseconds;
                double sp = t1 / tp;
                double ep = sp / p;
                Console.WriteLine($"{p,-5} | {tp,-7} | {sp, -7 : ##.####} | {ep, -7: ##.####}");
            }
            fileStream.Close();
        }

        static void ParseArgs(string[] args)
        {
            int idx = 0;
            if ((idx = Array.IndexOf(args, "-s")) != -1 ||
                (idx = Array.IndexOf(args, "-size")) != -1)
            {
                var screen = args[idx + 1].Split('x');
                width = int.Parse(screen[0]);
                height = int.Parse(screen[1]);
            }

            if ((idx = Array.IndexOf(args, "-r")) != -1 ||
                (idx = Array.IndexOf(args, "-rect")) != -1)
            {
                var scaleStr = args[idx + 1].Split(':');
                scale.Lx = double.Parse(scaleStr[0]);
                scale.Hx = double.Parse(scaleStr[1]);
                scale.Ly = double.Parse(scaleStr[2]);
                scale.Hy = double.Parse(scaleStr[3]);
            }

            if ((idx = Array.IndexOf(args, "-t")) != -1 ||
                (idx = Array.IndexOf(args, "-tasks")) != -1)
            {
                taskCnt = int.Parse(args[idx + 1]);
            }

            if((idx = Array.IndexOf(args, "-o")) != -1 ||
               (idx = Array.IndexOf(args, "-output")) != -1)
            {
                path = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + '\\' + args[idx + 1];
            }

            if (Array.IndexOf(args, "-q") != -1 || Array.IndexOf(args, "-quiet") != -1)
            {
                IsQuiet = true;
            } 

            if((idx = Array.IndexOf(args, "-g")) != -1 ||
               (idx = Array.IndexOf(args, "-granularity")) != -1)
            {
                granularity = int.Parse(args[idx + 1]);
            }

            if (Array.IndexOf(args, "-test") != -1)
            {
                IsQuiet = true;
                IsTest = true;
            }
        }

        static void Time(Action action, string function, int chunkSize, int threadCnt)
        {
            var sw = Stopwatch.StartNew();
            action();
            Console.WriteLine("Function                                           | Elapsed Time     | Chunk size | Threads count");
            Console.WriteLine("--------------------------------------------------------------------------------------------------");
            Console.WriteLine($"{function,-50} | {sw.Elapsed} | {chunkSize,-10} | {threadCnt} \n");
        }

        static void DrawMandelbrotSet(int width, int height, Stream stream, MandelbrotScale scale)
        {
            if(!IsQuiet)
            {
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} started drawing Mandelbrot. \n");
            }
            using (var image = new Image<Rgba32>(width, height))
            {
                for(int i = 0; i < width; i++)
                    MandelbrotInnerLoop(i, width, height, image, scale);

                image.SaveAsPng(stream);
            };
            if (!IsQuiet)
            {
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} finished drawing Mandelbrot. \n");
            }
        }

        static void DrawMandelbrotSetParallelNaive(int width, int height, Stream stream, int taskCnt, MandelbrotScale scale)
        {
            using (var image = new Image<Rgba32>(width, height))
            {
                CustomPartitionParallelFor(0, width, (i) =>
                {
                    MandelbrotInnerLoop(i, width, height, image, scale);
                }, taskCnt);

                image.SaveAsPng(stream);
            };
        }

        static void DrawMandelbrotSetParallel(int width, int height, Stream stream, int taskCnt, int chunkSize, MandelbrotScale scale)
        {
            if (!IsQuiet)
            {
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} started drawing Mandelbrot. \n");
            }
            using (var image = new Image<Rgba32>(width, height))
            {
                CustomChunkPartitionParallelFor(0, width, (i) =>
                {
                    MandelbrotInnerLoop(i, width, height, image, scale);
                }, chunkSize, taskCnt);

                image.SaveAsPng(stream);
            };
            if (!IsQuiet)
            {
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} finished drawing Mandelbrot. \n");
            }
        }

        static void DrawMandelbrotSetParallel2(int width, int height, Stream stream, int taskCnt, int chunkSize, MandelbrotScale scale)
        {
            using (var image = new Image<Rgba32>(width, height))
            {
                Parallel.ForEach(Partitioner.Create(0, width, chunkSize),
                    new ParallelOptions() { MaxDegreeOfParallelism = taskCnt },
                    range =>
                    {
                        for(int i = range.Item1; i < range.Item2; i++)
                            MandelbrotInnerLoop(i, width, height, image, scale);
                    });

                image.SaveAsPng(stream);
            };
        }

        static void MandelbrotInnerLoop(int i, int width, int height, Image<Rgba32> image, MandelbrotScale scale)
        {
            for (int j = 0; j < height; j++)
            {
                var cx = ((double)i).Map(0, width, scale.Lx, scale.Hx);
                var cy = ((double)j).Map(0, height, scale.Ly, scale.Hy);
                var c = new Complex(cx, cy);
                var iterations = CalculatePixel(c);

                if (iterations == maxIterations)
                    image[i, j] = new Rgba32(0, 0, 0);
                else
                {
                    var factor = iterations / (double)maxIterations;
                    var intensity = (byte)Math.Round(255 * factor * 10);
                    image[i, j] = new Rgba32(intensity, intensity, intensity);
                }
            }
        }

        static int CalculatePixel(Complex c)
        {
            var z = new Complex(0, 0);
            var it = 0;
            while (z.Real * z.Real + z.Imaginary * z.Imaginary <= 4 && it < maxIterations)
            {
                z = c*Complex.Cos(z);
                it++;
            }
            return it;
        }

        public static void CustomPartitionParallelFor(int inclusiveLowerBound, int exclusiveUpperBound,
            Action<int> body, int? threadCnt = null)
        {
            int numProcs = threadCnt.HasValue ? threadCnt.Value : Environment.ProcessorCount;
            int size = exclusiveUpperBound - inclusiveLowerBound;
            int range = size / numProcs;
            // Keep track of the number of threads remaining to complete.
            int remaining = numProcs;
            using (ManualResetEvent mre = new ManualResetEvent(false))
            {
                // Create each of the threads.
                for (int p = 0; p < numProcs; p++)
                {
                    int start = p * range + inclusiveLowerBound;
                    int end = (p == numProcs - 1) ? exclusiveUpperBound : start + range;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        for (int i = start; i < end; i++) body(i);
                        if (Interlocked.Decrement(ref remaining) == 0) mre.Set();
                    });
                }
                // Wait for all threads to complete.
                mre.WaitOne();
            }
        }

        static void CustomChunkPartitionParallelFor(int inclusiveLowerBound, int exclusiveUpperBound,
            Action<int> body, int chunkSize, int? threadCnt = null)
        {
            // Get the number of processors, initialize the number of remaining
            // threads, and set the starting point for the iteration.
            int numProcs = threadCnt.HasValue ? threadCnt.Value : Environment.ProcessorCount;
            int remainingWorkItems = numProcs;
            int nextIteration = inclusiveLowerBound;
            using (ManualResetEvent mre = new ManualResetEvent(false))
            {
                // Create each of the work items.
                for (int p = 0; p < numProcs; p++)
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        var sw = new Stopwatch();
                        if (!IsQuiet)
                        {
                            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} started calculating chunks. \n");
                            sw.Start();
                        }
                        int index;
                        while ((index = Interlocked.Add(ref nextIteration, chunkSize) - chunkSize) < exclusiveUpperBound)
                        {
                            int end = index + chunkSize;
                            if (end >= exclusiveUpperBound) end = exclusiveUpperBound;
                            for (int i = index; i < end; i++) body(i);
                        }
                        if (!IsQuiet)
                        {
                            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} finished calculating chunks. Elapsed time: {sw.ElapsedMilliseconds}ms.\n");
                        }
                        if (Interlocked.Decrement(ref remainingWorkItems) == 0)
                            mre.Set();
                    });
                }
                // Wait for all threads to complete
                mre.WaitOne();
            }
        }
    }
}
