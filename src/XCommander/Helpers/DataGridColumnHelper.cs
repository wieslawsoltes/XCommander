using System;
using Avalonia.Data.Core;

namespace XCommander.Helpers;

public static class DataGridColumnHelper
{
    public static IPropertyInfo CreateProperty<TItem, TValue>(
        string name,
        Func<TItem, TValue> getter,
        Action<TItem, TValue>? setter = null)
    {
        return new ClrPropertyInfo(
            name,
            target => getter((TItem)target),
            setter == null
                ? null
                : (target, value) => setter((TItem)target, value is null ? default! : (TValue)value),
            typeof(TValue));
    }
}
