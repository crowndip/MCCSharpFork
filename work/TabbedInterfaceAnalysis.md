# Tabbed Interface Analysis for Midnight Commander .NET

**Date:** 2026-04-07  
**Purpose:** Analyze feasibility of adding tabbed interface to file panels

---

## Current Architecture

### Panel Structure
- **Two panels**: `_leftPanelView` and `_rightPanelView` (FilePanelView instances)
- **Single directory per panel**: Each panel shows one directory at a time
- **Panel switching**: Tab key switches between left/right panels
- **No tab support**: Panels cannot show multiple directories simultaneously

### Existing Related Features

#### 1. **Hotlist/Favourites System**
- **HotlistManager**: Manages bookmarked directories
- **Hierarchical groups**: Supports nested folder groups
- **Quick access**: Ctrl+\ opens hotlist dialog
- **Add to hotlist**: Ctrl+X H adds current directory
- **Persistent storage**: ~/.config/mc/hotlist

#### 2. **Panel History**
- **Directory history**: Each panel maintains navigation history
- **Back/Forward**: Navigate through visited directories
- **No visual representation**: History is internal only

#### 3. **Overlay Modes**
- **Quick View**: Shows file preview in other panel
- **Info**: Shows file information in other panel
- **Tree**: Shows directory tree in other panel
- **Mechanism**: Replaces panel view temporarily

---

## Tabbed Interface Design Options

### Option 1: **Extend Favourites Menu as Tab Bar**

**Concept**: Convert favourites/hotlist into persistent tabs

**Pros:**
- ✅ Leverages existing hotlist infrastructure
- ✅ Minimal UI changes (add tab bar above panels)
- ✅ Persistent tabs (saved in hotlist file)
- ✅ Quick switching between favourite directories
- ✅ Hierarchical organization (groups as tab groups)

**Cons:**
- ❌ Confuses favourites with open tabs
- ❌ Limited to bookmarked directories only
- ❌ Cannot open arbitrary directories as tabs
- ❌ Doesn't match Total Commander behavior

**Implementation Complexity:** Low (2-3 days)

---

### Option 2: **True Tabbed Panels (Total Commander Style)**

**Concept**: Each panel can have multiple tabs showing different directories

**Architecture:**
```
McApplication
├── Left Panel Container
│   ├── Tab Bar (TabView or custom)
│   ├── Tab 1: FilePanelView → /home/user
│   ├── Tab 2: FilePanelView → /var/log
│   └── Tab 3: FilePanelView → /etc
└── Right Panel Container
    ├── Tab Bar
    ├── Tab 1: FilePanelView → /tmp
    └── Tab 2: FilePanelView → /usr/local
```

**Features:**
- Multiple tabs per panel (left and right independent)
- Open new tab (Ctrl+T)
- Close tab (Ctrl+W)
- Switch tabs (Ctrl+Tab / Ctrl+Shift+Tab)
- Drag tabs to reorder
- Drag tabs between panels
- Tab context menu (close, close others, close all)
- Tab persistence (save/restore on exit)

**Pros:**
- ✅ Matches Total Commander exactly
- ✅ Full flexibility (any directory in any tab)
- ✅ Independent left/right tab sets
- ✅ Familiar UX for TC users
- ✅ Powerful workflow (multiple directories open)

**Cons:**
- ❌ Significant architecture changes
- ❌ Complex state management
- ❌ Terminal.Gui TabView limitations
- ❌ Memory usage (multiple FilePanelView instances)
- ❌ Longer implementation time

**Implementation Complexity:** High (1-2 weeks)

---

### Option 3: **Hybrid: Favourites + Quick Tabs**

**Concept**: Favourites menu shows pinned tabs, plus temporary tabs for recent directories

**Architecture:**
```
Favourites Menu
├── Pinned Tabs (from hotlist)
│   ├── 📌 Home
│   ├── 📌 Projects
│   └── 📌 Downloads
├── ─────────────
└── Recent Tabs (auto-added)
    ├── /var/log
    ├── /etc
    └── /tmp
```

**Features:**
- Favourites menu shows both pinned and recent directories
- Click to switch panel to that directory
- Pin/unpin directories (adds to hotlist)
- Recent directories auto-added (max 10)
- Keyboard shortcuts (Alt+1..9 for first 9)
- Visual indicator for current directory

**Pros:**
- ✅ Minimal architecture changes
- ✅ Leverages existing hotlist
- ✅ Quick access to both favourites and recent
- ✅ No complex tab management
- ✅ Fast implementation

**Cons:**
- ❌ Not true tabs (just quick navigation)
- ❌ No visual tab bar
- ❌ Limited to one directory per panel at a time
- ❌ Doesn't match TC behavior

**Implementation Complexity:** Low (1-2 days)

---

## Recommended Approach

### **Phase 1: Hybrid Favourites + Quick Tabs** (Immediate)

**Why:**
- Quick to implement (1-2 days)
- Provides immediate value
- No breaking changes
- Foundation for future true tabs

**Implementation:**
1. Add "Recent Directories" section to Favourites menu
2. Track last 10 visited directories per panel
3. Add keyboard shortcuts (Alt+1..9)
4. Visual indicator for current directory
5. Pin/unpin functionality

**Code Changes:**
- `McApplication.cs`: Add recent directory tracking
- `HotlistManager.cs`: Add recent entries (non-persistent)
- Favourites menu: Add recent section
- Keyboard handler: Add Alt+1..9 shortcuts

---

### **Phase 2: True Tabbed Panels** (Future)

**Why:**
- Full Total Commander parity
- Better workflow for power users
- More complex, needs careful design

**Prerequisites:**
- Phase 1 completed (validates UX)
- Terminal.Gui TabView evaluation
- Memory profiling (multiple FilePanelView instances)
- State management design

**Implementation Plan:**
1. **Week 1: Architecture**
   - Design tab container structure
   - Evaluate Terminal.Gui TabView vs custom
   - Design state persistence
   - Memory optimization strategy

2. **Week 2: Core Implementation**
   - Tab container for each panel
   - Tab creation/destruction
   - Tab switching logic
   - Basic keyboard shortcuts

3. **Week 3: Advanced Features**
   - Tab drag-and-drop
   - Tab context menu
   - Tab persistence
   - Tab duplication

4. **Week 4: Polish & Testing**
   - Edge cases
   - Memory leak testing
   - Performance optimization
   - Documentation

---

## Technical Challenges

### 1. **Terminal.Gui TabView Limitations**
- TabView in Terminal.Gui v2 has API quirks
- May need custom tab bar implementation
- Tab rendering in TUI is limited

**Solution:** Custom tab bar using View + ListView

### 2. **Memory Management**
- Multiple FilePanelView instances consume memory
- Each panel loads directory listings
- Need lazy loading and disposal

**Solution:** 
- Lazy tab loading (load on switch)
- Dispose inactive tabs after timeout
- Limit max tabs (e.g., 10 per panel)

### 3. **State Persistence**
- Save/restore tab sets on exit
- Remember active tab per panel
- Handle invalid paths on restore

**Solution:**
- Extend config file with tab state
- Validate paths on restore
- Fallback to home directory

### 4. **Keyboard Shortcuts**
- Many shortcuts already used
- Need intuitive tab navigation
- Avoid conflicts with existing bindings

**Solution:**
- Ctrl+T (new tab)
- Ctrl+W (close tab)
- Ctrl+Tab / Ctrl+Shift+Tab (switch)
- Alt+1..9 (jump to tab)

---

## Minimal Implementation (Phase 1)

### Quick Tabs via Enhanced Favourites Menu

**Files to Modify:**
1. `McApplication.cs` (~100 lines)
   - Add recent directory tracking
   - Update Favourites menu builder
   - Add Alt+1..9 handlers

2. `HotlistManager.cs` (~50 lines)
   - Add RecentDirectories list
   - Add/remove recent entries
   - Limit to 10 entries

**New Features:**
- Recent directories in Favourites menu
- Alt+1..9 to jump to first 9 favourites/recent
- Visual indicator (★) for current directory
- Separator between pinned and recent

**Example Menu:**
```
Favourites
├── ★ Home                    Alt+1
├── Projects                  Alt+2
├── Downloads                 Alt+3
├── ─────────────────────
├── /var/log                  Alt+4
├── /etc                      Alt+5
└── /tmp                      Alt+6
```

**Implementation Time:** 1-2 days  
**Complexity:** Low  
**Value:** High (immediate productivity boost)

---

## Conclusion

### Recommendation: **Implement Phase 1 First**

**Rationale:**
1. **Quick win**: 1-2 days vs 1-2 weeks
2. **Low risk**: No architecture changes
3. **High value**: Immediate productivity improvement
4. **Foundation**: Validates UX before full tabs
5. **Reversible**: Can still do full tabs later

**Next Steps:**
1. Implement Phase 1 (enhanced favourites)
2. Gather user feedback
3. Evaluate Terminal.Gui TabView
4. Design Phase 2 architecture
5. Implement true tabs if needed

### Alternative: **Skip to Phase 2**

**Only if:**
- Users strongly demand true tabs
- Terminal.Gui TabView is proven stable
- Memory profiling shows acceptable overhead
- 1-2 weeks development time is acceptable

---

## Code Estimate

### Phase 1: Enhanced Favourites (~150 lines)
```csharp
// McApplication.cs
private List<string> _leftRecentDirs = new();
private List<string> _rightRecentDirs = new();

private void TrackRecentDirectory(bool left, string path)
{
    var recent = left ? _leftRecentDirs : _rightRecentDirs;
    recent.Remove(path);
    recent.Insert(0, path);
    if (recent.Count > 10) recent.RemoveAt(10);
}

private MenuItem[] BuildFavouritesMenu()
{
    var items = new List<MenuItem>();
    
    // Pinned (from hotlist)
    int idx = 1;
    foreach (var entry in _hotlist.Entries.Take(9))
    {
        items.Add(new MenuItem(
            $"{(IsCurrent(entry.Path) ? "★ " : "")}{entry.Label}",
            $"Alt+{idx}",
            () => NavigateTo(entry.Path)
        ));
        idx++;
    }
    
    // Separator
    if (_leftRecentDirs.Count > 0 || _rightRecentDirs.Count > 0)
        items.Add(null!);
    
    // Recent
    var recent = GetActivePanel() == _leftPanelView 
        ? _leftRecentDirs : _rightRecentDirs;
    foreach (var path in recent.Take(9 - idx + 1))
    {
        items.Add(new MenuItem(
            $"{(IsCurrent(path) ? "★ " : "")}{Path.GetFileName(path)}",
            idx <= 9 ? $"Alt+{idx}" : "",
            () => NavigateTo(path)
        ));
        idx++;
    }
    
    return items.ToArray();
}
```

### Phase 2: True Tabs (~1000+ lines)
- TabContainer class
- Tab state management
- Tab persistence
- Drag-and-drop
- Context menus
- Memory management

---

**Recommendation:** Start with Phase 1, evaluate, then decide on Phase 2.
