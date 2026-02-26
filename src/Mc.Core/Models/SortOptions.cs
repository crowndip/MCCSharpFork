namespace Mc.Core.Models;

public enum SortField
{
    Name,
    Extension,
    Size,
    ModificationTime,
    AccessTime,
    CreationTime,
    Permissions,
    Owner,
    Group,
    Inode,
    Unsorted,
}

public sealed class SortOptions
{
    public SortField Field { get; set; } = SortField.Name;
    public bool Descending { get; set; }
    public bool DirectoriesFirst { get; set; } = true;
    public bool CaseSensitive { get; set; } = OperatingSystem.IsLinux();
    public bool VersionSort { get; set; }   // Like ls --version-sort
}
