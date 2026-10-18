using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.LibraryLoader;

namespace IconPicker.Core.Icons;

/// <summary>
/// Reads the icons inside .exe and .dll files, ported from FolderIconNative in Set Folder Icons.ps1. Windows' own icon extraction only returns a blurry 32 px icon, so each icon is rebuilt from the file's resources with every size it contains (up to 256 px).
/// </summary>
/// <remarks>Icon N in <c>IconResource=file,N</c> is the Nth icon group in resource order; a negative N is the resource ID -N.</remarks>
public static class ExeIconExtractor
{
    private const int ResourceTypeIcon = 3;
    private const int ResourceTypeGroupIcon = 14;

    /// <summary>How many icons the file holds: 0 when it has none or can't be read.</summary>
    public static int CountIcons(string modulePath)
    {
        return WithModule(modulePath, module => GetIconGroupNames(module).Count);
    }

    /// <summary>The icon as the bytes of an .ico file holding every size it contains, or null when it is missing or damaged.</summary>
    public static byte[]? ExtractIconFile(string modulePath, int iconIndex)
    {
        return WithModule(modulePath, module =>
        {
            if (iconIndex < 0)
            {
                return -iconIndex <= ushort.MaxValue ? BuildIconFile(module, new ResourceName((ushort)-iconIndex, null)) : null;
            }
            var groupNames = GetIconGroupNames(module);
            return iconIndex < groupNames.Count ? BuildIconFile(module, groupNames[iconIndex]) : null;
        });
    }

    /// <summary>
    /// The icons that can be read, with their index and largest size, at most <paramref name="maximumCount"/> of them. Explorer shows the first one.
    /// </summary>
    public static IReadOnlyList<ModuleIcon> ListIcons(string modulePath, int maximumCount = int.MaxValue)
    {
        return WithModule(modulePath, module =>
        {
            var icons = new List<ModuleIcon>();
            var groupNames = GetIconGroupNames(module);
            for (var index = 0; index < groupNames.Count && icons.Count < maximumCount; index++)
            {
                var iconFile = BuildIconFile(module, groupNames[index]);
                var sizes = iconFile is null ? null : IcoFileBuilder.ReadImageSizes(iconFile);
                if (sizes is not null)
                {
                    icons.Add(new ModuleIcon(index, sizes.Max(size => Math.Max(size.Width, size.Height))));
                }
            }
            return (IReadOnlyList<ModuleIcon>)icons;
        }) ?? [];
    }

    /// <summary>The resource IDs of the file's icon groups, in order; named groups are left out. Used by tests to check negative indexes.</summary>
    internal static IReadOnlyList<ushort> GetIconGroupIds(string modulePath)
    {
        return WithModule(modulePath, module => (IReadOnlyList<ushort>)GetIconGroupNames(module).Where(name => name.Name is null).Select(name => name.Id).ToList()) ?? [];
    }

    private static T? WithModule<T>(string modulePath, Func<HMODULE, T> action)
    {
        using var module = PInvoke.LoadLibraryEx(modulePath, LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_AS_IMAGE_RESOURCE);
        if (module.IsInvalid)
        {
            return default;
        }
        return action((HMODULE)module.DangerousGetHandle());
    }

    private static unsafe List<ResourceName> GetIconGroupNames(HMODULE module)
    {
        var groupNames = new List<ResourceName>();
        // Resource names are only valid during the enumeration, so string names are copied right away.
        ENUMRESNAMEPROCW callback = (callbackModule, resourceType, resourceName, parameter) =>
        {
            groupNames.Add(ResourceName.From(resourceName));
            return true;
        };
        PInvoke.EnumResourceNames(module, IntResource(ResourceTypeGroupIcon), callback, 0);
        GC.KeepAlive(callback);
        return groupNames;
    }

    /// <summary>
    /// An icon group lists its sizes as 14-byte entries pointing to separate icon resources; an .ico file uses 16-byte entries pointing to offsets in the file.
    /// </summary>
    private static byte[]? BuildIconFile(HMODULE module, ResourceName groupName)
    {
        try
        {
            var groupBytes = groupName.Read(module, ResourceTypeGroupIcon);
            if (groupBytes is null || groupBytes.Length < 6)
            {
                return null;
            }
            int entryCount = BitConverter.ToUInt16(groupBytes, 4);
            var images = new List<IcoImage>(entryCount);
            for (var i = 0; i < entryCount; i++)
            {
                var entryOffset = 6 + i * 14;
                if (entryOffset + 14 > groupBytes.Length)
                {
                    break;
                }
                var iconId = BitConverter.ToUInt16(groupBytes, entryOffset + 12);
                var imageBytes = new ResourceName(iconId, null).Read(module, ResourceTypeIcon);
                if (imageBytes is null)
                {
                    continue;
                }
                // Width, height, colour count, reserved, planes and bit count are the same in both formats.
                images.Add(new IcoImage(groupBytes[entryOffset..(entryOffset + 8)], imageBytes));
            }
            return images.Count == 0 ? null : IcoFileBuilder.Build(images);
        }
        catch (Exception exception) when (exception is ArgumentException or IndexOutOfRangeException or OverflowException)
        {
            return null;
        }
    }

    private static unsafe PCWSTR IntResource(int resourceId)
    {
        return new PCWSTR((char*)resourceId);
    }

    /// <summary>A resource is named by a number (stored in the pointer itself) or by a string.</summary>
    private readonly record struct ResourceName(ushort Id, string? Name)
    {
        public static unsafe ResourceName From(PWSTR resourceName)
        {
            var pointerValue = (nuint)resourceName.Value;
            return pointerValue <= ushort.MaxValue ? new ResourceName((ushort)pointerValue, null) : new ResourceName(0, resourceName.ToString());
        }

        public unsafe byte[]? Read(HMODULE module, int resourceType)
        {
            if (Name is null)
            {
                return ReadResource(module, IntResource(Id), IntResource(resourceType));
            }
            fixed (char* namePointer = Name)
            {
                return ReadResource(module, new PCWSTR(namePointer), IntResource(resourceType));
            }
        }

        private static unsafe byte[]? ReadResource(HMODULE module, PCWSTR resourceName, PCWSTR resourceType)
        {
            var resourceInfo = PInvoke.FindResource(module, resourceName, resourceType);
            if (resourceInfo.IsNull)
            {
                return null;
            }
            var resourceSize = PInvoke.SizeofResource(module, resourceInfo);
            var resourceHandle = PInvoke.LoadResource(module, resourceInfo);
            if (resourceHandle.IsNull || resourceSize == 0)
            {
                return null;
            }
            var resourceData = PInvoke.LockResource(resourceHandle);
            if (resourceData is null)
            {
                return null;
            }
            return new ReadOnlySpan<byte>(resourceData, (int)resourceSize).ToArray();
        }
    }
}

/// <summary>An icon inside an .exe or .dll file.</summary>
/// <param name="Index">Its position among the file's icons, as used by <c>IconResource=file,Index</c>.</param>
/// <param name="LargestSize">Its largest size in pixels.</param>
public sealed record ModuleIcon(int Index, int LargestSize);
