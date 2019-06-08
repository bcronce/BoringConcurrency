using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BoringConcurrency.Common
{
    public static class InterlockedEnum<TEnum> where TEnum : struct, Enum
    {
        private static bool s_ValidType;

        static InterlockedEnum()
        {
            Type typeOfEnum = Enum.GetUnderlyingType(typeof(TEnum));
            s_ValidType = typeOfEnum == typeof(int);
        }

        public static TEnum CompareExchange(ref TEnum location1, TEnum value, TEnum comparand)
        {
            if (!s_ValidType) throw new InvalidCastException("Enum must be type int");

            int returnValue = Interlocked.CompareExchange(
                location1: ref Unsafe.As<TEnum, int>(ref location1)
                , value: Unsafe.As<TEnum, int>(ref value)
                , comparand: Unsafe.As<TEnum, int>(ref comparand)
            );
            return Unsafe.As<int, TEnum>(ref returnValue);
        }

        public static TEnum Exchange(ref TEnum location1, TEnum value)
        {
            if (!s_ValidType) throw new InvalidCastException("Enum must be type int");

            int returnValue = Interlocked.Exchange(
                location1: ref Unsafe.As<TEnum, int>(ref location1)
                , value: Unsafe.As<TEnum, int>(ref value)
            );
            return Unsafe.As<int, TEnum>(ref returnValue);
        }
    }
}
