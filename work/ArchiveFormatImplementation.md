# Archive Format Support Implementation

## Date: 2026-04-07

## Summary
Extended the `SevenZipVfsProvider` to support multiple archive formats that 7zip can handle, bringing the project closer to Total Commander's archive capabilities.

## Changes Made

### 1. **SevenZipVfsProvider.cs** - Extended Format Support
- **Schemes**: Added support for `rar`, `cab`, `iso`, `arj`, `lha`, `jar` schemes
- **Extensions**: Added detection for `.rar`, `.cab`, `.iso`, `.arj`, `.lha`, `.lzh`, `.jar`, `.ace`, `.gz`, `.bz2`, `.xz`, `.wim`, `.vhd`
- **Name**: Updated to "7-Zip Archive (multi-format)"
- **Path Handling**: Updated to preserve scheme when navigating within archives

### 2. **McApplication.cs** - Archive Entry Points
Added archive format detection for:
- `.rar` → RAR archives
- `.cab` → Cabinet archives
- `.iso` → ISO disk images
- `.arj` → ARJ archives
- `.lha`, `.lzh` → LHA/LZH archives
- `.jar` → Java archives

### 3. **FilePanelView.cs** - Visual Detection
Added archive extensions to syntax highlighting:
- `.arj`
- `.lha`
- `.lzh`
- `.jar`

### 4. **README.md** - Documentation
Updated VFS feature list to include: 7Z, RAR, CAB, ISO, ARJ, LHA, JAR

## Supported Archive Formats

### Now Supported (via 7zip CLI):
- ✅ **7Z** - 7-Zip native format
- ✅ **RAR** - WinRAR archives (read-only)
- ✅ **CAB** - Microsoft Cabinet files
- ✅ **ISO** - ISO 9660 disk images
- ✅ **ARJ** - ARJ compressed archives
- ✅ **LHA/LZH** - LHA compressed archives
- ✅ **JAR** - Java archives (ZIP-based)
- ✅ **ZIP** - Standard ZIP archives (already supported)
- ✅ **TAR** - Tape archives (already supported)
- ✅ **GZ/BZ2/XZ** - Compressed files (via 7zip)

### Previously Supported:
- ZIP (via ZipVfsProvider)
- TAR (via TarVfsProvider)
- CPIO (via CpioVfsProvider)

## Technical Details

### How It Works
1. User navigates to an archive file (e.g., `file.rar`)
2. `TryGetArchiveVfsPath()` detects the extension and creates a VFS path with appropriate scheme
3. `VfsRegistry` routes the request to `SevenZipVfsProvider`
4. Provider uses 7zip CLI to list/extract archive contents
5. User can navigate inside the archive like a regular directory

### Requirements
- 7zip must be installed on the system
- Executable must be in PATH or configured in settings
- Supported executables: `7z`, `7za`, `7zz`, or custom path

### Limitations
- **Read-only**: Archives cannot be modified via VFS
- **Performance**: Each navigation requires 7zip CLI execution
- **Password-protected**: Not yet supported (requires password prompt)
- **Multi-volume**: Not yet supported (requires special handling)

## Testing

### Build Status
✅ Solution builds successfully with no errors
⚠️ 2 warnings (unrelated to archive changes)

### Manual Testing Required
1. Test RAR archive browsing
2. Test CAB archive browsing
3. Test ISO image browsing
4. Test ARJ archive browsing
5. Test LHA/LZH archive browsing
6. Test JAR archive browsing
7. Verify file extraction from each format
8. Test navigation within nested directories
9. Test ".." navigation out of archives
10. Verify syntax highlighting for archive files

## Next Steps

### Potential Enhancements
1. **Password Support**: Add password prompt for encrypted archives
2. **Multi-volume**: Handle split archives (.rar.001, .rar.002, etc.)
3. **Archive Creation**: Allow creating archives (currently read-only)
4. **Progress Indicators**: Show progress for large archive operations
5. **Caching**: Cache archive listings to improve performance
6. **Archive Info**: Show archive properties (compression ratio, etc.)

### Related Features from Total Commander
- ❌ Self-extracting archive creation
- ❌ Archive conversion between formats
- ❌ Archive testing/verification
- ❌ Archive comments
- ❌ Solid archive support

## Impact

### User Benefits
- Browse RAR archives without external tools
- Access ISO images directly
- Work with legacy formats (ARJ, LHA)
- Consistent interface for all archive types

### Code Quality
- Minimal changes (4 files modified)
- Leverages existing 7zip infrastructure
- No new dependencies
- Maintains backward compatibility

## Conclusion

This implementation significantly expands archive format support by leveraging 7zip's multi-format capabilities. The changes are minimal, focused, and maintain the existing architecture. Users can now work with most common archive formats directly within the file manager.

The implementation follows the principle of "write only the absolute minimal amount of code needed" by reusing the existing `SevenZipVfsProvider` infrastructure rather than creating separate providers for each format.
