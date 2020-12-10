using System;
using System.Runtime.InteropServices;
using System.Security;

using NativeCodeSharp;
using NativeCodeSharp.Intrinsics;


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


        /// <summary>
        /// Native method handle of memory comparison function.
        /// </summary>
        private NativeMethodHandle<CompareMemoryDelegate> _compareMemoryMethodHandle;
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
            var mh = CreateAppropreateCompareMemoryMethodHandle();
            if (mh != null)
            {
                _compareMemoryMethodHandle = mh;
                _compareMemory = mh.Method;
            }
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
                if (_compareMemoryMethodHandle != null)
                {
                    _compareMemoryMethodHandle.Dispose();
                    _compareMemoryMethodHandle = null;
                }
            }
            IsDisposed = true;
        }


        /// <summary>
        /// Release all resources used by the <see cref="MemoryComparator"/> instance.
        /// </summary>
        public void Dispose()
        {
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
        public static unsafe bool CompareMemoryNaiveX64(IntPtr pData1, IntPtr  pData2, UIntPtr dataLength)
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
        public static unsafe bool CompareMemoryNaiveX86(IntPtr pData1, IntPtr  pData2, UIntPtr dataLength)
        {
            return CompareMemoryNaiveX64((byte*)pData1, (byte*)pData2, (uint)dataLength);
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
                    0x45, 0x89, 0xc2,                    // mov    r10d,r8d
                    0x41, 0x83, 0xea, 0x10,              // sub    r10d,0x10
                    0x78, 0x4b,                          // js     L3
                    0xf3, 0x0f, 0x6f, 0x01,              // movdqu xmm0,XMMWORD PTR [rcx]
                    0xf3, 0x0f, 0x6f, 0x12,              // movdqu xmm2,XMMWORD PTR [rdx]
                    0x66, 0x0f, 0x74, 0xc2,              // pcmpeqb xmm0,xmm2
                    0x66, 0x0f, 0xd7, 0xc0,              // pmovmskb eax,xmm0
                    0x3d, 0xff, 0xff, 0x00, 0x00,        // cmp    eax,0xffff
                    0x0f, 0x85, 0x7c, 0x00, 0x00, 0x00,  // jne    L7
                    0x41, 0xb9, 0x10, 0x00, 0x00, 0x00,  // mov    r9d,0x10
                    0xeb, 0x23,                          // jmp    L2
                    0x0f, 0x1f, 0x40, 0x00,              // nop    DWORD PTR [rax+0x0]
                    // L1:
                    0xf3, 0x42, 0x0f, 0x6f, 0x04, 0x0a,  // movdqu xmm0,XMMWORD PTR [rdx+r9*1]
                    0xf3, 0x42, 0x0f, 0x6f, 0x0c, 0x09,  // movdqu xmm1,XMMWORD PTR [rcx+r9*1]
                    0x49, 0x83, 0xc1, 0x10,              // add    r9,0x10
                    0x66, 0x0f, 0x74, 0xc1,              // pcmpeqb xmm0,xmm1
                    0x66, 0x0f, 0xd7, 0xc0,              // pmovmskb eax,xmm0
                    0x3d, 0xff, 0xff, 0x00, 0x00,        // cmp    eax,0xffff
                    0x75, 0x51,                          // jne    L7
                    // L2:
                    0x45, 0x39, 0xca,                    // cmp    r10d,r9d
                    0x7d, 0xdc,                          // jge    L1
                    // L3:
                    0x41, 0xf6, 0xc0, 0x0f,              // test   r8b,0xf
                    0xb8, 0x01, 0x00, 0x00, 0x00,        // mov    eax,0x1
                    0x74, 0x3b,                          // je     L6
                    0x45, 0x8d, 0x48, 0xf1,              // lea    r9d,[r8-0xf]
                    0x4d, 0x63, 0xc9,                    // movsxd r9,r9d
                    0x4d, 0x39, 0xc8,                    // cmp    r8,r9
                    0x76, 0x2f,                          // jbe    L6
                    0x42, 0x0f, 0xb6, 0x04, 0x0a,        // movzx  eax,BYTE PTR [rdx+r9*1]
                    0x42, 0x38, 0x04, 0x09,              // cmp    BYTE PTR [rcx+r9*1],al
                    0x75, 0x2a,                          // jne    L7
                    0x49, 0x8d, 0x41, 0x01,              // lea    rax,[r9+0x1]
                    0xeb, 0x14,                          // jmp    L5
                    0x0f, 0x1f, 0x40, 0x00,              // nop    DWORD PTR [rax+0x0]
                    // L4:
                    0x44, 0x0f, 0xb6, 0x0c, 0x01,        // movzx  r9d,BYTE PTR [rcx+rax*1]
                    0x48, 0x83, 0xc0, 0x01,              // add    rax,0x1
                    0x44, 0x3a, 0x4c, 0x02, 0xff,        // cmp    r9b,BYTE PTR [rdx+rax*1-0x1]
                    0x75, 0x10,                          // jne    L7
                    // L5:
                    0x49, 0x39, 0xc0,                    // cmp    r8,rax
                    0x75, 0xeb,                          // jne    L4
                    0xb8, 0x01, 0x00, 0x00, 0x00,        // mov    eax,0x1
                    // L6:
                    0xc3,                                // ret
                    0x0f, 0x1f, 0x44, 0x00, 0x00,        // nop    DWORD PTR [rax+rax*1+0x0]
                    // L7:
                    0x31, 0xc0,                          // xor    eax,eax
                    0xc3                                 // ret
                } : new byte[]
                {
                    0x55,                                // push   ebp
                    0x89, 0xe5,                          // mov    ebp,esp
                    0x57,                                // push   edi
                    0x56,                                // push   esi
                    0x8b, 0x75, 0x10,                    // mov    esi,DWORD PTR [ebp+0x10]
                    0x53,                                // push   ebx
                    0x8b, 0x4d, 0x0c,                    // mov    ecx,DWORD PTR [ebp+0xc]
                    0x8b, 0x5d, 0x08,                    // mov    ebx,DWORD PTR [ebp+0x8]
                    0x83, 0xe4, 0xf0,                    // and    esp,0xfffffff0
                    0x89, 0xf7,                          // mov    edi,esi
                    0x83, 0xef, 0x10,                    // sub    edi,0x10
                    0x78, 0x3b,                          // js     L3
                    0xf3, 0x0f, 0x6f, 0x03,              // movdqu xmm0,XMMWORD PTR [ebx]
                    0xf3, 0x0f, 0x6f, 0x11,              // movdqu xmm2,XMMWORD PTR [ecx]
                    0x66, 0x0f, 0x74, 0xc2,              // pcmpeqb xmm0,xmm2
                    0x66, 0x0f, 0xd7, 0xc0,              // pmovmskb eax,xmm0
                    0x3d, 0xff, 0xff, 0x00, 0x00,        // cmp    eax,0xffff
                    0x75, 0x53,                          // jne    L5
                    0x31, 0xd2,                          // xor    edx,edx
                    0xeb, 0x19,                          // jmp    L2
                    // L1:
                    0xf3, 0x0f, 0x6f, 0x04, 0x11,        // movdqu xmm0,XMMWORD PTR [ecx+edx*1]
                    0xf3, 0x0f, 0x6f, 0x0c, 0x13,        // movdqu xmm1,XMMWORD PTR [ebx+edx*1]
                    0x66, 0x0f, 0x74, 0xc1,              // pcmpeqb xmm0,xmm1
                    0x66, 0x0f, 0xd7, 0xc0,              // pmovmskb eax,xmm0
                    0x3d, 0xff, 0xff, 0x00, 0x00,        // cmp    eax,0xffff
                    0x75, 0x36,                          // jne    L5
                    // L2:
                    0x83, 0xc2, 0x10,                    // add    edx,0x10
                    0x39, 0xd7,                          // cmp    edi,edx
                    0x7d, 0xe0,                          // jge    L1
                    // L3:
                    0xf7, 0xc6, 0x0f, 0x00, 0x00, 0x00,  // test   esi,0xf
                    0xb8, 0x01, 0x00, 0x00, 0x00,        // mov    eax,0x1
                    0x74, 0x24,                          // je     L6
                    0x89, 0xf7,                          // mov    edi,esi
                    0x83, 0xc7, 0xf1,                    // add    edi,0xfffffff1
                    0x89, 0xfa,                          // mov    edx,edi
                    0x73, 0x1b,                          // jae    L6
                    0x0f, 0xb6, 0x04, 0x39,              // movzx  eax,BYTE PTR [ecx+edi*1]
                    0x38, 0x04, 0x3b,                    // cmp    BYTE PTR [ebx+edi*1],al
                    0x75, 0x10,                          // jne    L5
                    // L4:
                    0x83, 0xc2, 0x01,                    // add    edx,0x1
                    0x39, 0xf2,                          // cmp    edx,esi
                    0x74, 0x16,                          // je     L7
                    0x0f, 0xb6, 0x04, 0x11,              // movzx  eax,BYTE PTR [ecx+edx*1]
                    0x38, 0x04, 0x13,                    // cmp    BYTE PTR [ebx+edx*1],al
                    0x74, 0xf0,                          // je     L4
                    // L5:
                    0x31, 0xc0,                          // xor    eax,eax
                    // L6:
                    0x8d, 0x65, 0xf4,                    // lea    esp,[ebp-0xc]
                    0x5b,                                // pop    ebx
                    0x5e,                                // pop    esi
                    0x5f,                                // pop    edi
                    0x5d,                                // pop    ebp
                    0xc3,                                // ret
                    0x8d, 0x76, 0x00,                    // lea    esi,[esi+0x0]
                    // L7:
                    0x8d, 0x65, 0xf4,                    // lea    esp,[ebp-0xc]
                    0xb8, 0x01, 0x00, 0x00, 0x00,        // mov    eax,0x1
                    0x5b,                                // pop    ebx
                    0x5e,                                // pop    esi
                    0x5f,                                // pop    edi
                    0x5d,                                // pop    ebp
                    0xc3                                 // ret
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
                    0x45, 0x89, 0xc2,                          // mov    r10d,r8d
                    0x41, 0x83, 0xea, 0x20,                    // sub    r10d,0x20
                    0x78, 0x6c,                                // js     L3
                    0xc5, 0xfa, 0x6f, 0x01,                    // vmovdqu xmm0,XMMWORD PTR [rcx]
                    0xc5, 0xfa, 0x6f, 0x0a,                    // vmovdqu xmm1,XMMWORD PTR [rdx]
                    0xc4, 0xe3, 0x7d, 0x38, 0x41, 0x10, 0x01,  // vinserti128 ymm0,ymm0,XMMWORD PTR [rcx+0x10],0x1
                    0xc4, 0xe3, 0x75, 0x38, 0x4a, 0x10, 0x01,  // vinserti128 ymm1,ymm1,XMMWORD PTR [rdx+0x10],0x1
                    0xc5, 0xfd, 0x74, 0xc1,                    // vpcmpeqb ymm0,ymm0,ymm1
                    0xc5, 0xfd, 0xd7, 0xc0,                    // vpmovmskb eax,ymm0
                    0x83, 0xf8, 0xff,                          // cmp    eax,0xffffffff
                    0x0f, 0x85, 0x90, 0x00, 0x00, 0x00,        // jne    L7
                    0x41, 0xb9, 0x20, 0x00, 0x00, 0x00,        // mov    r9d,0x20
                    0xeb, 0x35,                                // jmp    L2
                    0x0f, 0x1f, 0x84, 0x00, 0x00, 0x00, 0x00,  // nop    DWORD PTR [rax+rax*1+0x0]
                    0x00,
                    // L1:
                    0xc4, 0xa1, 0x7a, 0x6f, 0x04, 0x0a,        // vmovdqu xmm0,XMMWORD PTR [rdx+r9*1]
                    0xc4, 0xa1, 0x7a, 0x6f, 0x0c, 0x09,        // vmovdqu xmm1,XMMWORD PTR [rcx+r9*1]
                    0xc4, 0xa3, 0x7d, 0x38, 0x44, 0x0a, 0x10,  // vinserti128 ymm0,ymm0,XMMWORD PTR [rdx+r9*1+0x10],0x1
                    0x01,
                    0xc4, 0xa3, 0x75, 0x38, 0x4c, 0x09, 0x10,  // vinserti128 ymm1,ymm1,XMMWORD PTR [rcx+r9*1+0x10],0x1
                    0x01,
                    0x49, 0x83, 0xc1, 0x20,                    // add    r9,0x20
                    0xc5, 0xfd, 0x74, 0xc1,                    // vpcmpeqb ymm0,ymm0,ymm1
                    0xc5, 0xfd, 0xd7, 0xc0,                    // vpmovmskb eax,ymm0
                    0x83, 0xf8, 0xff,                          // cmp    eax,0xffffffff
                    0x75, 0x53,                                // jne    L7
                    // L2:
                    0x45, 0x39, 0xca,                          // cmp    r10d,r9d
                    0x7d, 0xce,                                // jge    L1
                    0xc5, 0xf8, 0x77,                          // vzeroupper
                    // L3:
                    0x41, 0xf6, 0xc0, 0x1f,                    // test   r8b,0x1f
                    0xb8, 0x01, 0x00, 0x00, 0x00,              // mov    eax,0x1
                    0x74, 0x3a,                                // je     L6
                    0x45, 0x8d, 0x48, 0xe1,                    // lea    r9d,[r8-0x1f]
                    0x4d, 0x63, 0xc9,                          // movsxd r9,r9d
                    0x4d, 0x39, 0xc8,                          // cmp    r8,r9
                    0x76, 0x2e,                                // jbe    L6
                    0x42, 0x0f, 0xb6, 0x04, 0x0a,              // movzx  eax,BYTE PTR [rdx+r9*1]
                    0x42, 0x38, 0x04, 0x09,                    // cmp    BYTE PTR [rcx+r9*1],al
                    0x75, 0x39,                                // jne    L8
                    0x49, 0x8d, 0x41, 0x01,                    // lea    rax,[r9+0x1]
                    0xeb, 0x13,                                // jmp    L5
                    0x0f, 0x1f, 0x00,                          // nop    DWORD PTR [rax]
                    // L4:
                    0x44, 0x0f, 0xb6, 0x0c, 0x01,              // movzx  r9d,BYTE PTR [rcx+rax*1]
                    0x48, 0x83, 0xc0, 0x01,                    // add    rax,0x1
                    0x44, 0x3a, 0x4c, 0x02, 0xff,              // cmp    r9b,BYTE PTR [rdx+rax*1-0x1]
                    0x75, 0x20,                                // jne    L8
                    // L5:
                    0x49, 0x39, 0xc0,                          // cmp    r8,rax
                    0x75, 0xeb,                                // jne    L4
                    0xb8, 0x01, 0x00, 0x00, 0x00,              // mov    eax,0x1
                    // L6:
                    0xc3,                                      // ret
                    0x0f, 0x1f, 0x44, 0x00, 0x00,              // nop    DWORD PTR [rax+rax*1+0x0]
                    // L7:
                    0x31, 0xc0,                                // xor    eax,eax
                    0xc5, 0xf8, 0x77,                          // vzeroupper
                    0xc3,                                      // ret
                    0x66, 0x2e, 0x0f, 0x1f, 0x84, 0x00, 0x00,  // nop    WORD PTR cs:[rax+rax*1+0x0]
                    0x00, 0x00, 0x00,
                    // L8:
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
                    0x8b, 0x4d, 0x0c,                          // mov    ecx,DWORD PTR [ebp+0xc]
                    0x8b, 0x5d, 0x08,                          // mov    ebx,DWORD PTR [ebp+0x8]
                    0x83, 0xe4, 0xe0,                          // and    esp,0xffffffe0
                    0x89, 0xf7,                                // mov    edi,esi
                    0x83, 0xef, 0x20,                          // sub    edi,0x20
                    0x78, 0x58,                                // js     L3
                    0xc5, 0xfa, 0x6f, 0x23,                    // vmovdqu xmm4,XMMWORD PTR [ebx]
                    0xc5, 0xfa, 0x6f, 0x29,                    // vmovdqu xmm5,XMMWORD PTR [ecx]
                    0xc4, 0xe3, 0x5d, 0x38, 0x43, 0x10, 0x01,  // vinserti128 ymm0,ymm4,XMMWORD PTR [ebx+0x10],0x1
                    0xc4, 0xe3, 0x55, 0x38, 0x49, 0x10, 0x01,  // vinserti128 ymm1,ymm5,XMMWORD PTR [ecx+0x10],0x1
                    0xc5, 0xfd, 0x74, 0xc1,                    // vpcmpeqb ymm0,ymm0,ymm1
                    0xc5, 0xfd, 0xd7, 0xc0,                    // vpmovmskb eax,ymm0
                    0x83, 0xf8, 0xff,                          // cmp    eax,0xffffffff
                    0x75, 0x74,                                // jne    L7
                    0x31, 0xd2,                                // xor    edx,edx
                    0xeb, 0x27,                                // jmp    L2
                    // L1:
                    0xc5, 0xfa, 0x6f, 0x14, 0x11,              // vmovdqu xmm2,XMMWORD PTR [ecx+edx*1]
                    0xc5, 0xfa, 0x6f, 0x1c, 0x13,              // vmovdqu xmm3,XMMWORD PTR [ebx+edx*1]
                    0xc4, 0xe3, 0x6d, 0x38, 0x44, 0x11, 0x10,  // vinserti128 ymm0,ymm2,XMMWORD PTR [ecx+edx*1+0x10],0x1
                    0x01,
                    0xc4, 0xe3, 0x65, 0x38, 0x4c, 0x13, 0x10,  // vinserti128 ymm1,ymm3,XMMWORD PTR [ebx+edx*1+0x10],0x1
                    0x01,
                    0xc5, 0xfd, 0x74, 0xc1,                    // vpcmpeqb ymm0,ymm0,ymm1
                    0xc5, 0xfd, 0xd7, 0xc0,                    // vpmovmskb eax,ymm0
                    0x83, 0xf8, 0xff,                          // cmp    eax,0xffffffff
                    0x75, 0x49,                                // jne    L7
                    // L2:
                    0x83, 0xc2, 0x20,                          // add    edx,0x20
                    0x39, 0xd7,                                // cmp    edi,edx
                    0x7d, 0xd2,                                // jge    L1
                    0xc5, 0xf8, 0x77,                          // vzeroupper
                    // L3:
                    0xf7, 0xc6, 0x1f, 0x00, 0x00, 0x00,        // test   esi,0x1f
                    0xb8, 0x01, 0x00, 0x00, 0x00,              // mov    eax,0x1
                    0x74, 0x24,                                // je     L6
                    0x89, 0xf7,                                // mov    edi,esi
                    0x83, 0xc7, 0xe1,                          // add    edi,0xffffffe1
                    0x89, 0xfa,                                // mov    edx,edi
                    0x73, 0x1b,                                // jae    L6
                    0x0f, 0xb6, 0x04, 0x39,                    // movzx  eax,BYTE PTR [ecx+edi*1]
                    0x38, 0x04, 0x3b,                          // cmp    BYTE PTR [ebx+edi*1],al
                    0x75, 0x10,                                // jne    L5
                    // L4:
                    0x83, 0xc2, 0x01,                          // add    edx,0x1
                    0x39, 0xf2,                                // cmp    edx,esi
                    0x74, 0x29,                                // je     L8
                    0x0f, 0xb6, 0x04, 0x11,                    // movzx  eax,BYTE PTR [ecx+edx*1]
                    0x38, 0x04, 0x13,                          // cmp    BYTE PTR [ebx+edx*1],al
                    0x74, 0xf0,                                // je     L4
                    // L5:
                    0x31, 0xc0,                                // xor    eax,eax
                    // L6:
                    0x8d, 0x65, 0xf4,                          // lea    esp,[ebp-0xc]
                    0x5b,                                      // pop    ebx
                    0x5e,                                      // pop    esi
                    0x5f,                                      // pop    edi
                    0x5d,                                      // pop    ebp
                    0xc3,                                      // ret
                    0x8d, 0xb6, 0x00, 0x00, 0x00, 0x00,        // lea    esi,[esi+0x0]
                    // L7:
                    0x31, 0xc0,                                // xor    eax,eax
                    0xc5, 0xf8, 0x77,                          // vzeroupper
                    0x8d, 0x65, 0xf4,                          // lea    esp,[ebp-0xc]
                    0x5b,                                      // pop    ebx
                    0x5e,                                      // pop    esi
                    0x5f,                                      // pop    edi
                    0x5d,                                      // pop    ebp
                    0xc3,                                      // ret
                    0x8d, 0x76, 0x00,                          // lea    esi,[esi+0x0]
                    // L8:
                    0xb8, 0x01, 0x00, 0x00, 0x00,              // mov    eax,0x1
                    0xeb, 0xdb                                 // jmp    L6
                });
        }
    }
}
