# Feather Setup Instructions

## 🚨 Important: .js File Import Setup

If dropping `.js` files in the inspector doesn't work immediately, follow these steps:

### Step 1: Force Unity to Recognize JavaScript Files

1. **Restart Unity** after installing Feather
2. Or manually refresh: `Assets > Refresh` (Ctrl/Cmd + R)
3. Or use `Feather > Debug > Reimport All JS Files`

### Step 2: Verify JavaScript File Import

1. Create a test `.js` file or check existing ones
2. Look in the Project window - `.js` files should show as `TextAsset` with a document icon
3. Use `Feather > Debug > Test JS File Import` to verify they're imported correctly

### Step 3: Test Drag & Drop

#### Method A: Direct GameObject Drag & Drop (Recommended)
1. Create an empty GameObject
2. Drag a `.js` file from Project window onto the GameObject in **Hierarchy**
3. ScriptBehaviour should be automatically added

#### Method B: Scene View Drag & Drop  
1. Open Scene view
2. Drag a `.js` file from Project window onto any GameObject in the 3D scene
3. ScriptBehaviour should be automatically added

#### Method C: Manual Component Assignment
1. Add a ScriptBehaviour component: `Component > Feather > JavaScript Behaviour`
2. Drag a `.js` file into the "JavaScript File" field in the inspector
3. Properties should auto-detect

### Step 4: If Still Not Working

#### Check File Association
- Ensure your `.js` files are imported as `TextAsset`, not `MonoScript`
- If they show as `MonoScript`, Unity's default JavaScript handler is interfering

#### Force Override (if needed)
- Rename your files to `.jsu` or `.jsfeather` extensions
- These extensions are guaranteed to work with Feather

#### Debug Steps
1. `Feather > Debug > Check Runtime` - Verify Runtime exists in scene
2. `Feather > Debug > List All ScriptBehaviours` - Check existing components
3. `Feather > Debug > Test JS File Import` - Verify import system

## 🔧 Troubleshooting

### Problem: .js files show as MonoScript instead of TextAsset
**Solution**: Unity's legacy JavaScript support is interfering
- Use `.jsu` extension instead: `MyScript.jsu`
- Or disable Unity's default JavaScript importer (advanced)

### Problem: Drag & drop doesn't work
**Solution**: Multiple approaches
1. Try dragging onto GameObject in **Hierarchy** window
2. Try dragging onto GameObject in **Scene View** (3D scene)
3. Use Component menu: `Component > Feather > JavaScript Behaviour`
4. Manually assign in the JavaScript File field

### Problem: Properties don't auto-detect
**Solution**: Check JavaScript syntax
- Ensure class extends `jsBehaviour`
- Use correct decorator syntax: `@Light` not `@light`
- Click "Auto-Detect Properties" button in inspector

### Problem: Runtime errors
**Solution**: Verify setup
- Ensure Feather Runtime exists in scene (drag from prefab or add manually)
- Check JavaScript syntax is valid
- Verify all property fields are assigned in inspector

## ✅ Quick Test

1. Create `TestBehaviour.js`:
```javascript
class TestBehaviour extends jsBehaviour {
    @Light
    myLight;
    
    Awake() {
        Unity.Debug.Log('Test script working!');
    }
}
```

2. Drag it onto a GameObject in Hierarchy
3. Assign a Light to the "My Light" field
4. Play - should see log message

## 📁 File Extensions

- **`.js`** - Standard JavaScript (may conflict with Unity's legacy support)
- **`.jsu`** - JavaScript Unity (recommended if `.js` has issues)
- **`.jsfeather`** - Feather JavaScript (always works)

## 🔄 Alternative Workflow

If drag & drop still doesn't work:

1. **Add Component First**:
   - Select GameObject
   - `Component > Feather > JavaScript Behaviour`

2. **Assign Script**:
   - Click the circle next to "JavaScript File"
   - Select your `.js` file from the picker

3. **Configure Properties**:
   - Properties auto-detect when script is assigned
   - Assign Unity objects to the property fields

This workflow always works regardless of file association issues!
