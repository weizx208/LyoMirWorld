using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MirCommon.Network
{
    
    
    
    public class ZeroCopyBufferTestRunner
    {
        
        
        
        public static async Task Run()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("零拷贝缓冲区测试套件");
            Console.WriteLine("========================================");
            Console.WriteLine();

            var test = new ZeroCopyBufferTest();

            try
            {
                await test.RunAllTests();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试运行失败: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("测试套件运行完成");
            Console.WriteLine("========================================");
        }
    }

    
    
    
    public class ZeroCopyBufferTest
    {
        
        
        
        public async Task RunPerformanceTest()
        {
            Console.WriteLine("=== 零拷贝缓冲区性能测试 ===");
            Console.WriteLine();

            
            await TestManagedMemory();

            Console.WriteLine();

            
            await TestUnmanagedMemory();

            Console.WriteLine();
            Console.WriteLine("=== 性能测试完成 ===");
        }

        
        
        
        private async Task TestManagedMemory()
        {
            Console.WriteLine("测试1：托管内存性能测试");
            Console.WriteLine("------------------------");

            using var buffer = new ZeroCopyBuffer(useUnmanagedMemory: false);
            await RunBufferTest(buffer, "托管内存");
        }

        
        
        
        private async Task TestUnmanagedMemory()
        {
            Console.WriteLine("测试2：非托管内存性能测试");
            Console.WriteLine("------------------------");

            using var buffer = new ZeroCopyBuffer(useUnmanagedMemory: true);
            await RunBufferTest(buffer, "非托管内存");
        }

        
        
        
        private async Task RunBufferTest(ZeroCopyBuffer buffer, string testName)
        {
            const int iterations = 100000;
            const int bufferSize = 4096;

            Console.WriteLine($"测试配置：迭代次数={iterations}, 缓冲区大小={bufferSize}字节");
            Console.WriteLine();

            
            for (int i = 0; i < 1000; i++)
            {
                var block = buffer.Rent(bufferSize);
                buffer.Return(block);
            }

            
            var stopwatch = Stopwatch.StartNew();
            long totalMemory = 0;

            for (int i = 0; i < iterations; i++)
            {
                var block = buffer.Rent(bufferSize);
                totalMemory += block.Size;

                
                var span = block.AsSpan();
                span[0] = (byte)(i & 0xFF);
                span[span.Length - 1] = (byte)((i >> 8) & 0xFF);

                buffer.Return(block);
            }

            stopwatch.Stop();

            
            var stats = buffer.GetStatistics();

            Console.WriteLine($"测试结果 ({testName}):");
            Console.WriteLine($"  总耗时: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  平均每次操作: {stopwatch.ElapsedMilliseconds / (double)iterations:F3}ms");
            Console.WriteLine($"  总分配内存: {totalMemory / 1024 / 1024}MB");
            Console.WriteLine($"  缓冲池统计:");
            Console.WriteLine($"    总分配块数: {stats.totalBlocks}");
            Console.WriteLine($"    池中块数: {stats.pooledBlocks}");
            Console.WriteLine($"    总分配字节数: {stats.totalBytes / 1024}KB");
            Console.WriteLine($"    内存类型: {(stats.useUnmanagedMemory ? "非托管内存" : "托管内存")}");

            
            await TestSegmentCreation(buffer, testName);
        }

        
        
        
        private async Task TestSegmentCreation(ZeroCopyBuffer buffer, string testName)
        {
            const int segmentTestIterations = 10000;
            const int dataSize = 1024;

            Console.WriteLine();
            Console.WriteLine($"  内存段创建测试 ({testName}):");

            
            var testData = new byte[dataSize];
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = (byte)(i & 0xFF);
            }

            
            var stopwatch = Stopwatch.StartNew();
            long totalMemory = 0;

            for (int i = 0; i < segmentTestIterations; i++)
            {
                var (block, memory) = buffer.CreateSegment(testData, 0, testData.Length);
                totalMemory += block.Size;

                
                var span = memory.Span;
                if (span[0] != testData[0] || span[span.Length - 1] != testData[testData.Length - 1])
                {
                    Console.WriteLine($"    错误：数据验证失败！");
                }

                buffer.Return(block);
            }

            stopwatch.Stop();

            Console.WriteLine($"    从数组创建内存段:");
            Console.WriteLine($"      总耗时: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"      平均每次操作: {stopwatch.ElapsedMilliseconds / (double)segmentTestIterations:F3}ms");
            Console.WriteLine($"      总分配内存: {totalMemory / 1024 / 1024}MB");

            
            stopwatch.Restart();
            totalMemory = 0;

            var testSpan = new ReadOnlySpan<byte>(testData);

            for (int i = 0; i < segmentTestIterations; i++)
            {
                var (block, memory) = buffer.CreateSegment(testSpan);
                totalMemory += block.Size;

                
                var span = memory.Span;
                if (span[0] != testData[0] || span[span.Length - 1] != testData[testData.Length - 1])
                {
                    Console.WriteLine($"    错误：数据验证失败！");
                }

                buffer.Return(block);
            }

            stopwatch.Stop();

            Console.WriteLine($"    从Span创建内存段:");
            Console.WriteLine($"      总耗时: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"      平均每次操作: {stopwatch.ElapsedMilliseconds / (double)segmentTestIterations:F3}ms");
            Console.WriteLine($"      总分配内存: {totalMemory / 1024 / 1024}MB");
        }

        
        
        
        public async Task RunConcurrencyTest()
        {
            Console.WriteLine("=== 零拷贝缓冲区并发测试 ===");
            Console.WriteLine();

            using var buffer = new ZeroCopyBuffer(useUnmanagedMemory: false);

            const int threadCount = 8;
            const int iterationsPerThread = 10000;
            const int bufferSize = 2048;

            Console.WriteLine($"测试配置：线程数={threadCount}, 每线程迭代次数={iterationsPerThread}, 缓冲区大小={bufferSize}字节");
            Console.WriteLine();

            var tasks = new Task[threadCount];
            var stopwatch = Stopwatch.StartNew();

            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        var block = buffer.Rent(bufferSize);

                        
                        var span = block.AsSpan();
                        span[0] = (byte)(i & 0xFF);
                        span[1] = (byte)((i >> 8) & 0xFF);

                        buffer.Return(block);
                    }
                });
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            var stats = buffer.GetStatistics();

            Console.WriteLine($"并发测试结果:");
            Console.WriteLine($"  总耗时: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  总操作数: {threadCount * iterationsPerThread}");
            Console.WriteLine($"  吞吐量: {threadCount * iterationsPerThread / (stopwatch.ElapsedMilliseconds / 1000.0):F0} 操作/秒");
            Console.WriteLine($"  缓冲池统计:");
            Console.WriteLine($"    总分配块数: {stats.totalBlocks}");
            Console.WriteLine($"    池中块数: {stats.pooledBlocks}");
            Console.WriteLine($"    总分配字节数: {stats.totalBytes / 1024}KB");
        }

        
        
        
        public async Task RunMemoryLeakTest()
        {
            Console.WriteLine("=== 零拷贝缓冲区内存泄漏测试 ===");
            Console.WriteLine();

            using var buffer = new ZeroCopyBuffer(useUnmanagedMemory: false);

            const int testCycles = 10;
            const int buffersPerCycle = 1000;
            const int bufferSize = 4096;

            Console.WriteLine($"测试配置：测试周期={testCycles}, 每周期缓冲区数={buffersPerCycle}, 缓冲区大小={bufferSize}字节");
            Console.WriteLine();

            var initialStats = buffer.GetStatistics();

            for (int cycle = 0; cycle < testCycles; cycle++)
            {
                
                var blocks = new ZeroCopyBuffer.BufferBlock[buffersPerCycle];
                for (int i = 0; i < buffersPerCycle; i++)
                {
                    blocks[i] = buffer.Rent(bufferSize);
                }

                
                for (int i = 0; i < buffersPerCycle; i++)
                {
                    var span = blocks[i].AsSpan();
                    span[0] = (byte)(cycle & 0xFF);
                }

                
                for (int i = 0; i < buffersPerCycle / 2; i++)
                {
                    buffer.Return(blocks[i]);
                    blocks[i] = null;
                }

                
                buffer.Cleanup(maxAgeMinutes: 0); 

                await Task.Delay(100); 
            }

            var finalStats = buffer.GetStatistics();

            Console.WriteLine($"内存泄漏测试结果:");
            Console.WriteLine($"  初始状态:");
            Console.WriteLine($"    总分配块数: {initialStats.totalBlocks}");
            Console.WriteLine($"    池中块数: {initialStats.pooledBlocks}");
            Console.WriteLine($"  最终状态:");
            Console.WriteLine($"    总分配块数: {finalStats.totalBlocks}");
            Console.WriteLine($"    池中块数: {finalStats.pooledBlocks}");
            Console.WriteLine($"  内存增长: {finalStats.totalBlocks - initialStats.totalBlocks} 块");
            Console.WriteLine($"  池增长: {finalStats.pooledBlocks - initialStats.pooledBlocks} 块");

            if (finalStats.totalBlocks > initialStats.totalBlocks + 100)
            {
                Console.WriteLine("  警告：检测到可能的内存泄漏！");
            }
            else
            {
                Console.WriteLine("  通过：未检测到明显的内存泄漏。");
            }
        }

        
        
        
        public async Task RunAllTests()
        {
            try
            {
                await RunPerformanceTest();
                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine();

                await RunConcurrencyTest();
                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine();

                await RunMemoryLeakTest();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试失败: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}
