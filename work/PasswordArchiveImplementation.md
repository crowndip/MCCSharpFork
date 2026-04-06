# Password-Protected Archive Support Implementation

## Date: 2026-04-07

## Summary
Implemented password-protected archive support for all archive formats handled by 7zip, and made 7zip the primary handler for ZIP files when available (with fallback to built-in .NET ZIP support).

## Changes Made

### 1. **SevenZipVfsProvider.cs** - Password Support
- **Password Cache**: Added dictionary to cache passwords per archive
- **Password Prompt**: Implemented Terminal.Gui dialog for password input
- **Exec Method**: Enhanced to handle password-protected archives
  - Try cached password first
  - Try without password
  - Prompt user if needed
  - Cache successful passwords
- **ReadEntries**: Updated to use password-aware Exec
- **OpenRead**: Updated to use password-aware Exec
- **ZIP Support**: Added "zip" scheme and ".zip" extension handling

### 2. **AppSetup.cs** - Provider Priority
- **Registration Order**: Moved `SevenZipVfsProvider` BEFORE `ZipVfsProvider`
- **Fallback Logic**: ZipVfsProvider now acts as fallback when 7zip not available
- **Comments**: Updated to clarify the priority system

### 3. **Mc.Vfs.Archives.csproj** - Dependencies
- **Terminal.Gui**: Added package reference for password dialog

## How It Works

### Password Flow
1. User attempts to access password-protected archive
2. 7zip returns error (exit code 2)
3. System prompts user for password via Terminal.Gui dialog
4. Password is cached for the archive session
5. Subsequent operations use cached password

### ZIP File Handling
1. When 7zip is installed:
   - `SevenZipVfsProvider` handles ZIP files
   - Supports password-protected ZIPs
   - Uses 7zip CLI for all operations
2. When 7zip is NOT installed:
   - `ZipVfsProvider` handles ZIP files
   - Uses built-in .NET System.IO.Compression
   - No password support (limitation of .NET library)

## Supported Features

### Password-Protected Archives
- ✅ **ZIP** - Password-protected ZIP files
- ✅ **RAR** - Password-protected RAR archives
- ✅ **7Z** - Password-protected 7-Zip archives
- ✅ **All other formats** - Any format 7zip supports with passwords

### Password Dialog Features
- ✅ **Secret input** - Password field with masked characters
- ✅ **Cancel support** - User can cancel password prompt
- ✅ **Password caching** - Passwords cached per archive during session
- ✅ **Archive name display** - Shows which archive needs password

## Technical Details

### Password Caching
```csharp
private readonly Dictionary<string, string> _passwordCache = new();
```
- Passwords stored per archive path
- Cache cleared when provider disposed
- No persistent storage (security consideration)

### 7zip Password Syntax
```bash
7z l -slt archive.zip -pMyPassword
```
- `-p` flag followed immediately by password
- No space between flag and password
- Password passed as command-line argument

### Error Handling
- Graceful fallback if password wrong
- User can retry or cancel
- Clear error messages
- No password exposure in logs

## Security Considerations

### ✅ Implemented
- Passwords not logged or displayed
- Secret input field (masked characters)
- In-memory only (no disk storage)
- Cache cleared on provider disposal

### ⚠️ Limitations
- Passwords passed as command-line arguments (visible in process list)
- No encryption of cached passwords in memory
- No password strength validation
- No password manager integration

### 🔒 Recommendations for Production
1. Consider using stdin for password input to 7zip
2. Implement secure string handling
3. Add password manager integration
4. Implement password expiration/timeout
5. Add audit logging for password attempts

## Testing

### Build Status
✅ Solution builds successfully with no errors or warnings

### Manual Testing Required
1. Test password-protected ZIP files
2. Test password-protected RAR files
3. Test password-protected 7Z files
4. Test wrong password handling
5. Test cancel button functionality
6. Test password caching (multiple file access)
7. Test fallback to ZipVfsProvider when 7zip not installed
8. Test non-password archives still work
9. Verify password field masking
10. Test with special characters in passwords

## Comparison with Total Commander

### Total Commander Features
- ✅ Password-protected archives
- ✅ Password caching
- ✅ Multiple archive formats
- ❌ Password manager integration
- ❌ Master password
- ❌ Password hints

### Our Implementation
- ✅ Password-protected archives (all 7zip formats)
- ✅ Password caching (session-based)
- ✅ Multiple archive formats (ZIP, RAR, 7Z, etc.)
- ❌ Password manager integration (not implemented)
- ❌ Master password (not implemented)
- ❌ Password hints (not implemented)

## Benefits

### User Experience
- Seamless password prompting
- No need to extract archives manually
- Password caching reduces repeated prompts
- Consistent interface across all archive types

### Technical Benefits
- Leverages existing 7zip infrastructure
- Minimal code changes
- No new external dependencies (except Terminal.Gui)
- Maintains backward compatibility

### Security
- Passwords not stored persistently
- Secret input field prevents shoulder surfing
- User control over password entry

## Next Steps

### Potential Enhancements
1. **Stdin Password Input**: Pass passwords via stdin instead of command-line
2. **Password Manager**: Integrate with system password manager
3. **Master Password**: Encrypt cached passwords with master password
4. **Password Timeout**: Clear cached passwords after inactivity
5. **Password Hints**: Allow users to set password hints
6. **Keyfile Support**: Support keyfile authentication for archives

### Related Features
- ❌ Multi-volume password-protected archives
- ❌ Self-extracting password-protected archives
- ❌ Archive encryption (creating password-protected archives)
- ❌ Password recovery/hints

## Conclusion

This implementation provides comprehensive password-protected archive support by leveraging 7zip's capabilities. The solution is minimal, secure, and user-friendly. By making 7zip the primary ZIP handler, we gain password support for ZIP files while maintaining backward compatibility through the fallback to .NET's built-in ZIP support.

The password dialog integrates seamlessly with the existing Terminal.Gui interface, and the caching mechanism reduces user friction while maintaining reasonable security practices.

---
*Implementation completed: 2026-04-07*
