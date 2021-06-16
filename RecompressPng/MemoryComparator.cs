#if NETCOREAPP3_0_OR_GREATER
#    define NET_SIMD
#endif  // NETCOREAPP3_0_OR_GREATER
using System;
using System.Runtime.InteropServices;
using System.Security;

#if NET_SIMD
using System.Runtime.Intrinsics.X86;
#else
using NativeCodeSharp;
using NativeCodeSharp.Intrinsics;
#endif  // NET_SIMD


namespace RecompressPng
{
    /// <summary>
    /// Delegate of two byte data comparison method.
    /// </summary>
    /// <param name="pData1">First pointer to byte data array.</param>
    /// <param name="pData2">Second pointer to byte data array.</param>
    /// <param name="dataLength">Data length of <paramref name="pData1"/> and <paramref name="pData2"/>.</param>
    /// <returns>True if two byte data is same, otherwise false.</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    [SuppressUnmanagedCodeSecurity]
    public delegate bool CompareMemoryDelegate([In] IntPtr pData1, [In] IntPtr pData2, [In] UIntPtr dataLength);


    /// <summary>
    /// Memory comparator class.
    /// </summary>
    public class MemoryComparator : IDisposable
    {
        /// <summary>
        /// A flag property which indicates this instance is disposed or not.
        /// </summary>
        public bool IsDisposed { get; private set; }


#if !NET_SIMD
        /// <summary>
        /// Native method handle of memory comparison function.
        /// </summary>
        private NativeMethodHandle<CompareMemoryDelegate> _compareMemoryMethodHandle;
#endif  // !NET_SIMD
        /// <summary>
        /// Delegate of memory comparison method.
        /// </summary>
        private CompareMemoryDelegate _compareMemory;


        /// <summary>
        /// Try to appropreate 
        /// </summary>
        public MemoryComparator()
        {
            IsDisposed = false;
#if NET_SIMD
            if (Avx2.IsSupported)
            {
                _compareMemory = CompareMemoryAvx2;
            }
            else if (Sse2.IsSupported)
            {
                _compareMemory = CompareMemorySse2;
            }
#else
            var mh = CreateAppropreateCompareMemoryMethodHandle();
            if (mh != null)
            {
                _compareMemoryMethodHandle = mh;
                _compareMemory = mh.Method;
            }
#endif  // NET_SIMD
            else if (Environment.Is64BitProcess)
            {
                _compareMemory = CompareMemoryNaiveX64;
            }
            else
            {
                _compareMemory = CompareMemoryNaiveX86;
            }
        }


        #region IDisposable Support
        /// <summary>
        /// Release resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
        /// <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed)
            {
                return;
            }
            if (disposing)
            {
                _compareMemory = null;
#if !NET_SIMD
                if (_compareMemoryMethodHandle != null)
                {
                    _compareMemoryMethodHandle.Dispose();
                    _compareMemoryMethodHandle = null;
                }
#endif  // !NET_SIMD
            }
            IsDisposed = true;
        }


        /// <summary>
        /// Release all resources used by the <see cref="MemoryComparator"/> instance.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }
        #endregion


        /// <summary>
        /// Compare two byte data.
        /// </summary>
        /// <param name="data1">First byte data array.</param>
        /// <param name="data2">Second byte data array.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        public bool CompareMemory(byte[] data1, byte[] data2)
        {
            return data1.LongLength == data2.LongLength && CompareMemory(data1, data2, data1.LongLength);
        }


        /// <summary>
        /// Compare two byte data.
        /// </summary>
        /// <param name="data1">First byte data array.</param>
        /// <param name="data2">Second byte data array.</param>
        /// <param name="dataLength">Data length of <paramref name="data1"/> and <paramref name="data2"/>.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        public unsafe bool CompareMemory(byte[] data1, byte[] data2, long dataLength)
        {
            fixed (byte* pData1 = data1)
            fixed (byte* pData2 = data2)
            {
                return CompareMemory((IntPtr)pData1, (IntPtr)pData2, data1.LongLength);
            }
        }


        /// <summary>
        /// Compare two byte data.
        /// </summary>
        /// <param name="pData1">First pointer to byte data array.</param>
        /// <param name="pData2">Second pointer to byte data array.</param>
        /// <param name="dataLength">Data length of <paramref name="pData1"/> and <paramref name="pData2"/>.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        public bool CompareMemory(IntPtr pData1, IntPtr pData2, long dataLength)
        {
            return _compareMemory(pData1, pData2, (UIntPtr)dataLength);
        }


        /// <summary>
        /// Compare two byte data for x64 environment.
        /// </summary>
        /// <param name="pData1">First pointer to byte data array.</param>
        /// <param name="pData2">Second pointer to byte data array.</param>
        /// <param name="dataLength">Data length of <paramref name="pData1"/> and <paramref name="pData2"/>.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        public static unsafe bool CompareMemoryNaiveX64(IntPtr pData1, IntPtr pData2, UIntPtr dataLength)
        {
            return CompareMemoryNaiveX64((byte*)pData1, (byte*)pData2, (ulong)dataLength);
        }


        /// <summary>
        /// Compare two byte data for x64 environment.
        /// </summary>
        /// <param name="pData1">First pointer to byte data array.</param>
        /// <param name="pData2">Second pointer to byte data array.</param>
        /// <param name="dataLength">Data length of <paramref name="pData1"/> and <paramref name="pData2"/>.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        public static unsafe bool CompareMemoryNaiveX64(byte* pData1, byte* pData2, ulong dataLength)
        {
            const ulong stride = sizeof(ulong);
            var n = dataLength / stride * stride;

            for (ulong i = 0; i < n; i += stride)
            {
                if (*(ulong*)&pData1[i] != *(ulong*)&pData2[i])
                {
                    return false;
                }
            }

            for (ulong i = n; i < dataLength; i++)
            {
                if (pData1[i] != pData2[i])
                {
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Compare two byte data for x86 environment.
        /// </summary>
        /// <param name="pData1">First pointer to byte data array.</param>
        /// <param name="pData2">Second pointer to byte data array.</param>
        /// <param name="dataLength">Data length of <paramref name="pData1"/> and <paramref name="pData2"/>.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        public static unsafe bool CompareMemoryNaiveX86(IntPtr pData1, IntPtr pData2, UIntPtr dataLength)
        {
            return CompareMemoryNaiveX86((byte*)pData1, (byte*)pData2, (uint)dataLength);
        }


        /// <summary>
        /// Compare two byte data for x86 environment.
        /// </summary>
        /// <param name="pData1">First pointer to byte data array.</param>
        /// <param name="pData2">Second pointer to byte data array.</param>
        /// <param name="dataLength">Data length of <paramref name="pData1"/> and <paramref name="pData2"/>.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        public static unsafe bool CompareMemoryNaiveX86(byte* pData1, byte* pData2, uint dataLength)
        {
            const uint stride = sizeof(uint);
            var n = dataLength / stride * stride;

            for (uint i = 0; i < n; i += stride)
            {
                if (*(uint*)&pData1[i] != *(uint*)&pData2[i])
                {
                    return false;
                }
            }

            for (uint i = n; i < dataLength; i++)
            {
                if (pData1[i] != pData2[i])
                {
                    return false;
                }
            }

            return true;
        }


#if NET_SIMD
        /// <summary>
        /// Compare two byte data using SSE2 instrunctions.
        /// </summary>
        /// <param name="pData1">First pointer to byte data array.</param>
        /// <param name="pData2">Second pointer to byte data array.</param>
        /// <param name="dataLength">Data length of <paramref name="pData1"/> and <paramref name="pData2"/>.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        public static unsafe bool CompareMemorySse2(IntPtr pData1, IntPtr pData2, UIntPtr dataLength)
        {
            return CompareMemorySse2((byte*)pData1, (byte*)pData2, (uint)dataLength);
        }


        /// <summary>
        /// Compare two byte data using SSE2 instrunctions.
        /// </summary>
        /// <param name="pData1">First pointer to byte data array.</param>
        /// <param name="pData2">Second pointer to byte data array.</param>
        /// <param name="dataLength">Data length of <paramref name="pData1"/> and <paramref name="pData2"/>.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        public static unsafe bool CompareMemorySse2(byte* pData1, byte* pData2, ulong dataLength)
        {
            const ulong stride = 16;
            var n = dataLength / stride * stride;

            for (ulong i = 0; i < n; i += stride)
            {
                if (Sse2.MoveMask(
                    Sse2.CompareEqual(
                        Sse2.LoadVector128(&pData1[i]),
                        Sse2.LoadVector128(&pData2[i]))) != 0xffff)
                {
                    return false;
                }
            }

            for (ulong i = n; i < dataLength; i++)
            {
                if (pData1[i] != pData2[i])
                {
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Compare two byte data using AVX2 instrunctions.
        /// </summary>
        /// <param name="pData1">First pointer to byte data array.</param>
        /// <param name="pData2">Second pointer to byte data array.</param>
        /// <param name="dataLength">Data length of <paramref name="pData1"/> and <paramref name="pData2"/>.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        public static unsafe bool CompareMemoryAvx2(IntPtr pData1, IntPtr pData2, UIntPtr dataLength)
        {
            return CompareMemoryAvx2((byte*)pData1, (byte*)pData2, (uint)dataLength);
        }


        /// <summary>
        /// Compare two byte data using AVX2 instrunctions.
        /// </summary>
        /// <param name="pData1">First pointer to byte data array.</param>
        /// <param name="pData2">Second pointer to byte data array.</param>
        /// <param name="dataLength">Data length of <paramref name="pData1"/> and <paramref name="pData2"/>.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        public static unsafe bool CompareMemoryAvx2(byte* pData1, byte* pData2, ulong dataLength)
        {
            const ulong stride = 32;
            var n = dataLength / stride * stride;

            for (ulong i = 0; i < n; i += stride)
            {
                if (Avx2.MoveMask(
                    Avx2.CompareEqual(
                        Avx.LoadVector256(&pData1[i]),
                        Avx.LoadVector256(&pData2[i]))) != -1)
                {
                    return false;
                }
            }

            for (ulong i = n; i < dataLength; i++)
            {
                if (pData1[i] != pData2[i])
                {
                    return false;
                }
            }

            return true;
        }

#else

        /// <summary>
        /// <para>Create native method handle of appropreate memory compare function using SIMD instruction
        /// which is available on the execute environment.</para>
        /// <para>If CPUID is not supported or SSE2 is not available, return null.</para>
        /// </summary>
        /// <returns>Created native method handle.</returns>
        private static NativeMethodHandle<CompareMemoryDelegate> CreateAppropreateCompareMemoryMethodHandle()
        {
            if (!Intrinsic.IsCpuIdSupported())
            {
                return null;
            }

            var feature = SimdUtil.GetCpuSimdFeature();
            if (feature.HasAvx2)
            {
                return CreateCompareMemoryAvx2MethodHandle();
            }
            else if (feature.HasSse2)
            {
                return CreateCompareMemorySse2MethodHandle();
            }
            else
            {
                return null;
            }
        }


        /// <summary>
        /// Create native method handle of  memory compare function using SSE2.
        /// </summary>
        /// <returns>Created native method handle.</returns>
        private static NativeMethodHandle<CompareMemoryDelegate> CreateCompareMemorySse2MethodHandle()
        {
            return NativeMethodHandle.Create<CompareMemoryDelegate>(Environment.Is64BitProcess ? new byte[]
                {
                    0x4d, 0x89, 0xc1,              // mov    r9,r8
                    0x49, 0x83, 0xe1, 0xf0,        // and    r9,0xfffffffffffffff0
                    0x74, 0x2f,                    // je     L4
                    0x45, 0x31, 0xd2,              // xor    r10d,r10d
                    0x31, 0xc0,                    // xor    eax,eax
                    0xeb, 0x0c,                    // jmp    L2
                    // L1:
                    0x41, 0x83, 0xc2, 0x10,        // add    r10d,0x10
                    0x49, 0x63, 0xc2,              // movsxd rax,r10d
                    0x4c, 0x39, 0xc8,              // cmp    rax,r9
                    0x73, 0x1c,                    // jae    L4
                    // L2:
                    0xf3, 0x0f, 0x6f, 0x04, 0x02,  // movdqu xmm0,XMMWORD PTR [rdx+rax*1]
                    0xf3, 0x0f, 0x6f, 0x0c, 0x01,  // movdqu xmm1,XMMWORD PTR [rcx+rax*1]
                    0x66, 0x0f, 0x74, 0xc1,        // pcmpeqb xmm0,xmm1
                    0x66, 0x0f, 0xd7, 0xc0,        // pmovmskb eax,xmm0
                    0x3d, 0xff, 0xff, 0x00, 0x00,  // cmp    eax,0xffff
                    0x74, 0xdb,                    // je     L1
                    0x31, 0xc0,                    // xor    eax,eax
                    // L3:
                    0xc3,                          // ret
                    // L4:
                    0x49, 0x63, 0xc1,              // movsxd rax,r9d
                    0x49, 0x39, 0xc0,              // cmp    r8,rax
                    0x77, 0x0f,                    // ja     L7
                    // L5:
                    0xb8, 0x01, 0x00, 0x00, 0x00,  // mov    eax,0x1
                    0xc3,                          // ret
                    // L6:
                    0x48, 0x83, 0xc0, 0x01,        // add    rax,0x1
                    0x49, 0x39, 0xc0,              // cmp    r8,rax
                    0x76, 0xf1,                    // jbe    L5
                    // L7:
                    0x44, 0x0f, 0xb6, 0x1c, 0x02,  // movzx  r11d,BYTE PTR [rdx+rax*1]
                    0x44, 0x38, 0x1c, 0x01,        // cmp    BYTE PTR [rcx+rax*1],r11b
                    0x74, 0xec,                    // je     L6
                    0x31, 0xc0,                    // xor    eax,eax
                    0xeb, 0xd9                     // jmp    L3
                } : new byte[]
                {
                    0x55,                          // push   ebp
                    0x89, 0xe5,                    // mov    ebp,esp
                    0x57,                          // push   edi
                    0x56,                          // push   esi
                    0x8b, 0x75, 0x10,              // mov    esi,DWORD PTR [ebp+0x10]
                    0x53,                          // push   ebx
                    0x8b, 0x4d, 0x08,              // mov    ecx,DWORD PTR [ebp+0x8]
                    0x89, 0xf7,                    // mov    edi,esi
                    0x8b, 0x5d, 0x0c,              // mov    ebx,DWORD PTR [ebp+0xc]
                    0x83, 0xe7, 0xf0,              // and    edi,0xfffffff0
                    0x74, 0x2b,                    // je     L4
                    0x31, 0xd2,                    // xor    edx,edx
                    0xeb, 0x07,                    // jmp    L2
                    // L1:
                    0x83, 0xc2, 0x10,              // add    edx,0x10
                    0x39, 0xd7,                    // cmp    edi,edx
                    0x76, 0x20,                    // jbe    L4
                    // L2:
                    0xf3, 0x0f, 0x6f, 0x04, 0x13,  // movdqu xmm0,XMMWORD PTR [ebx+edx*1]
                    0xf3, 0x0f, 0x6f, 0x0c, 0x11,  // movdqu xmm1,XMMWORD PTR [ecx+edx*1]
                    0x66, 0x0f, 0x74, 0xc1,        // pcmpeqb xmm0,xmm1
                    0x66, 0x0f, 0xd7, 0xc0,        // pmovmskb eax,xmm0
                    0x3d, 0xff, 0xff, 0x00, 0x00,  // cmp    eax,0xffff
                    0x74, 0xe0,                    // je     L1
                    // L3:
                    0x5b,                          // pop    ebx
                    0x31, 0xc0,                    // xor    eax,eax
                    0x5e,                          // pop    esi
                    0x5f,                          // pop    edi
                    0x5d,                          // pop    ebp
                    0xc3,                          // ret
                    // L4:
                    0x89, 0xf8,                    // mov    eax,edi
                    0x39, 0xfe,                    // cmp    esi,edi
                    0x77, 0x11,                    // ja     L7
                    // L5:
                    0x5b,                          // pop    ebx
                    0xb8, 0x01, 0x00, 0x00, 0x00,  // mov    eax,0x1
                    0x5e,                          // pop    esi
                    0x5f,                          // pop    edi
                    0x5d,                          // pop    ebp
                    0xc3,                          // ret
                    // L6:
                    0x83, 0xc0, 0x01,              // add    eax,0x1
                    0x39, 0xc6,                    // cmp    esi,eax
                    0x76, 0xef,                    // jbe    L5
                    // L7:
                    0x0f, 0xb6, 0x14, 0x03,        // movzx  edx,BYTE PTR [ebx+eax*1]
                    0x38, 0x14, 0x01,              // cmp    BYTE PTR [ecx+eax*1],dl
                    0x74, 0xf0,                    // je     L6
                    0xeb, 0xd7                     // jmp    L3
                });
        }


        /// <summary>
        /// Create native method handle of  memory compare function using AVX2.
        /// </summary>
        /// <returns>Created native method handle.</returns>
        private static NativeMethodHandle<CompareMemoryDelegate> CreateCompareMemoryAvx2MethodHandle()
        {
            return NativeMethodHandle.Create<CompareMemoryDelegate>(Environment.Is64BitProcess ? new byte[]
                {
                    0x4d, 0x89, 0xc1,                          // mov    r9,r8
                    0x49, 0x83, 0xe1, 0xe0,                    // and    r9,0xffffffffffffffe0
                    0x74, 0x43,                                // je     L4
                    0x45, 0x31, 0xd2,                          // xor    r10d,r10d
                    0x31, 0xc0,                                // xor    eax,eax
                    0xeb, 0x0c,                                // jmp    L2
                    // L1:
                    0x41, 0x83, 0xc2, 0x20,                    // add    r10d,0x20
                    0x49, 0x63, 0xc2,                          // movsxd rax,r10d
                    0x4c, 0x39, 0xc8,                          // cmp    rax,r9
                    0x73, 0x2d,                                // jae    L3
                    // L2:
                    0xc5, 0xfa, 0x6f, 0x14, 0x02,              // vmovdqu xmm2,XMMWORD PTR [rdx+rax*1]
                    0xc5, 0xfa, 0x6f, 0x1c, 0x01,              // vmovdqu xmm3,XMMWORD PTR [rcx+rax*1]
                    0xc4, 0xe3, 0x6d, 0x38, 0x44, 0x02, 0x10,  // vinserti128 ymm0,ymm2,XMMWORD PTR [rdx+rax*1+0x10],0x1
                    0x01,
                    0xc4, 0xe3, 0x65, 0x38, 0x4c, 0x01, 0x10,  // vinserti128 ymm1,ymm3,XMMWORD PTR [rcx+rax*1+0x10],0x1
                    0x01,
                    0xc5, 0xfd, 0x74, 0xc1,                    // vpcmpeqb ymm0,ymm0,ymm1
                    0xc5, 0xfd, 0xd7, 0xc0,                    // vpmovmskb eax,ymm0
                    0x83, 0xf8, 0xff,                          // cmp    eax,0xffffffff
                    0x74, 0xcd,                                // je     L1
                    0x31, 0xc0,                                // xor    eax,eax
                    0xc5, 0xf8, 0x77,                             // vzeroupper
                    0xc3,                                      // ret
                    // L3:
                    0xc5, 0xf8, 0x77,                          // vzeroupper
                    // L4:
                    0x49, 0x63, 0xc1,                          // movsxd rax,r9d
                    0x49, 0x39, 0xc0,                          // cmp    r8,rax
                    0x77, 0x0f,                                // ja     L7
                    // L5:
                    0xb8, 0x01, 0x00, 0x00, 0x00,              // mov    eax,0x1
                    0xc3,                                      // ret
                    // L6:
                    0x48, 0x83, 0xc0, 0x01,                    // add    rax,0x1
                    0x49, 0x39, 0xc0,                          // cmp    r8,rax
                    0x76, 0xf1,                                // jbe    L5
                    // L7:
                    0x44, 0x0f, 0xb6, 0x1c, 0x02,              // movzx  r11d,BYTE PTR [rdx+rax*1]
                    0x44, 0x38, 0x1c, 0x01,                    // cmp    BYTE PTR [rcx+rax*1],r11b
                    0x74, 0xec,                                // je     L6
                    0x31, 0xc0,                                // xor    eax,eax
                    0xc3                                       // ret
                } : new byte[]
                {
                    0x55,                                      // push   ebp
                    0x89, 0xe5,                                // mov    ebp,esp
                    0x57,                                      // push   edi
                    0x56,                                      // push   esi
                    0x8b, 0x75, 0x10,                          // mov    esi,DWORD PTR [ebp+0x10]
                    0x53,                                      // push   ebx
                    0x8b, 0x4d, 0x08,                          // mov    ecx,DWORD PTR [ebp+0x8]
                    0x89, 0xf7,                                // mov    edi,esi
                    0x8b, 0x5d, 0x0c,                          // mov    ebx,DWORD PTR [ebp+0xc]
                    0x83, 0xe7, 0xe0,                          // and    edi,0xffffffe0
                    0x74, 0x3f,                                // je     L4
                    0x31, 0xd2,                                // xor    edx,edx
                    0xeb, 0x07,                                // jmp    L2
                    // L1:
                    0x83, 0xc2, 0x20,                          // add    edx,0x20
                    0x39, 0xd7,                                // cmp    edi,edx
                    0x76, 0x31,                                // jbe    L3
                    // L2:
                    0xc5, 0xfa, 0x6f, 0x14, 0x13,              // vmovdqu xmm2,XMMWORD PTR [ebx+edx*1]
                    0xc5, 0xfa, 0x6f, 0x1c, 0x11,              // vmovdqu xmm3,XMMWORD PTR [ecx+edx*1]
                    0xc4, 0xe3, 0x6d, 0x38, 0x44, 0x13, 0x10,  // vinserti128 ymm0,ymm2,XMMWORD PTR [ebx+edx*1+0x10],0x1
                    0x01,
                    0xc4, 0xe3, 0x65, 0x38, 0x4c, 0x11, 0x10,  // vinserti128 ymm1,ymm3,XMMWORD PTR [ecx+edx*1+0x10],0x1
                    0x01,
                    0xc5, 0xfd, 0x74, 0xc1,                    // vpcmpeqb ymm0,ymm0,ymm1
                    0xc5, 0xfd, 0xd7, 0xc0,                    // vpmovmskb eax,ymm0
                    0x83, 0xf8, 0xff,                          // cmp    eax,0xffffffff
                    0x74, 0xd2,                                // je     L1
                    0x31, 0xc0,                                // xor    eax,eax
                    0xc5, 0xf8, 0x77,                          // vzeroupper
                    0x5b,                                      // pop    ebx
                    0x5e,                                      // pop    esi
                    0x5f,                                      // pop    edi
                    0x5d,                                      // pop    ebp
                    0xc3,                                      // ret
                    // L3:
                    0xc5, 0xf8, 0x77,                          // vzeroupper
                    // L4:
                    0x89, 0xf8,                                // mov    eax,edi
                    0x39, 0xfe,                                // cmp    esi,edi
                    0x77, 0x11,                                // ja     L7
                    // L5:
                    0x5b,                                      // pop    ebx
                    0xb8, 0x01, 0x00, 0x00, 0x00,              // mov    eax,0x1
                    0x5e,                                      // pop    esi
                    0x5f,                                      // pop    edi
                    0x5d,                                      // pop    ebp
                    0xc3,                                      // ret
                    // L6:
                    0x83, 0xc0, 0x01,                          // add    eax,0x1
                    0x39, 0xc6,                                // cmp    esi,eax
                    0x76, 0xef,                                // jbe    L5
                    // L7:
                    0x0f, 0xb6, 0x14, 0x03,                    // movzx  edx,BYTE PTR [ebx+eax*1]
                    0x38, 0x14, 0x01,                          // cmp    BYTE PTR [ecx+eax*1],dl
                    0x74, 0xf0,                                // je     L6
                    0x5b,                                      // pop    ebx
                    0x31, 0xc0,                                // xor    eax,eax
                    0x5e,                                      // pop    esi
                    0x5f,                                      // pop    edi
                    0x5d,                                      // pop    ebp
                    0xc3                                       // ret
                });
        }
#endif  // NET_SIMD
    }
}
