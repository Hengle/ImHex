﻿#pragma warning disable SYSLIB1054

using System.Drawing;
using System.Runtime.InteropServices;

namespace ImHex
{
    public interface IProvider
    {
        void readRaw(UInt64 address, IntPtr buffer, UInt64 size)
        {
            unsafe
            {
                Span<byte> data = new(buffer.ToPointer(), (int)size);
                read(address, data);
            }
        }
        
        void writeRaw(UInt64 address, IntPtr buffer, UInt64 size)
        {
            unsafe
            {
                ReadOnlySpan<byte> data = new(buffer.ToPointer(), (int)size);
                write(address, data);
            }
        }
        
        void read(UInt64 address, Span<byte> data);
        void write(UInt64 address, ReadOnlySpan<byte> data);

        UInt64 getSize();

        string getTypeName();
        string getName();
    }
    public class Memory
    {
        private static List<IProvider> _registeredProviders = new();
        private static List<Delegate> _registeredProviderDelegates = new();

        private delegate void DataAccessDelegate(UInt64 address, IntPtr buffer, UInt64 size);
        private delegate UInt64 GetSizeDelegate();
        
        [DllImport(Library.Name)]
        private static extern void readMemoryV1(UInt64 address, UInt64 size, IntPtr buffer);

        [DllImport(Library.Name)]
        private static extern void writeMemoryV1(UInt64 address, UInt64 size, IntPtr buffer);

        [DllImport(Library.Name)]
        private static extern bool getSelectionV1(IntPtr start, IntPtr end);
        
        [DllImport(Library.Name)]
        private static extern int registerProviderV1([MarshalAs(UnmanagedType.LPStr)] string typeName, [MarshalAs(UnmanagedType.LPStr)] string name, IntPtr readFunction, IntPtr writeFunction, IntPtr getSizeFunction);


        public static byte[] Read(ulong address, ulong size)
        {
            byte[] bytes = new byte[size];

            unsafe
            {
                fixed (byte* buffer = bytes)
                {
                    readMemoryV1(address, size, (IntPtr)buffer);
                }
            }


            return bytes;
        }

        public static void Write(ulong address, byte[] bytes)
        {
            unsafe
            {
                fixed (byte* buffer = bytes)
                {
                    writeMemoryV1(address, (UInt64)bytes.Length, (IntPtr)buffer);
                }
            }
        }

        public static (UInt64, UInt64)? GetSelection()
        {
            unsafe
            {
                UInt64 start = 0, end = 0;
                if (!getSelectionV1((nint)(&start), (nint)(&end)))
                {
                    return null;
                }

                return (start, end);
            }
        }
        
        public static int RegisterProvider<T>() where T : IProvider, new()
        {
            _registeredProviders.Add(new T());
            
            ref var provider = ref CollectionsMarshal.AsSpan(_registeredProviders)[^1];
            
            _registeredProviderDelegates.Add(new DataAccessDelegate(provider.readRaw));
            _registeredProviderDelegates.Add(new DataAccessDelegate(provider.writeRaw));
            _registeredProviderDelegates.Add(new GetSizeDelegate(provider.getSize));
            
            return registerProviderV1(
                _registeredProviders[^1].getTypeName(), 
                _registeredProviders[^1].getName(), 
                Marshal.GetFunctionPointerForDelegate(_registeredProviderDelegates[^3]), 
                Marshal.GetFunctionPointerForDelegate(_registeredProviderDelegates[^2]),
                Marshal.GetFunctionPointerForDelegate(_registeredProviderDelegates[^1])
            );
        }

    }
}
