# Feather Direct JavaScript File Support

This guide explains how to use JavaScript files directly with GameObjects without generating MonoBehaviours.

## ✅ What You Can Do

- **Drag & Drop**: Drop `.js` files directly onto GameObjects in the hierarchy
- **Automatic Setup**: ScriptBehaviour component is automatically added and configured
- **Property Auto-Detection**: JavaScript properties with decorators are automatically detected
- **Inspector Properties**: All JavaScript properties show up in the Unity inspector
- **No Code Generation**: No MonoBehaviours are dynamically generated

## 🎯 Quick Start

### Method 1: Drag & Drop (Easiest)

1. **Create a JavaScript file**:
```javascript
class MyBehaviour extends jsBehaviour {
    @Light
    myLight;
    
    @Text
    statusText;
    
    Update() {
        if (Unity.Input.GetKeyDown(Unity.KeyCode.Space)) {
            this.myLight.enabled = !this.myLight.enabled;
            this.statusText.text = this.myLight.enabled ? "Light ON" : "Light OFF";
        }
    }
}
```

2. **Drag the `.js` file onto any GameObject** in the hierarchy
3. **Auto-magic happens**:
   - ScriptBehaviour component is automatically added
   - Property bindings are auto-detected and created
   - Inspector shows your JavaScript properties

4. **Assign Unity objects** to the property fields in the inspector

### Method 2: Component Menu

1. Select a GameObject
2. Go to `Component > Feather > JavaScript Behaviour`
3. Assign your `.js` file to the "JavaScript File" field
4. Properties are automatically detected and shown

### Method 3: Create from Scratch

1. `GameObject > Feather > Create JavaScript GameObject`
2. `Assets > Create > Feather > JavaScript Behaviour`
3. Write your JavaScript class
4. Drag the created `.js` file onto the GameObject

## 📁 Supported File Extensions

- **`.js`** - Standard JavaScript (recommended)
- **`.jsu`** - JavaScript Unity (alternative)
- **`.jsfeather`** - Feather JavaScript (alternative)

## 🔧 How It Works

### The ScriptBehaviour Component

- **Single Component**: One `ScriptBehaviour` handles all JavaScript execution
- **Property Bindings**: Automatic detection of `@Decorator` properties
- **Inspector Integration**: Properties show up as normal Unity fields
- **Runtime Execution**: JavaScript runs through Jint engine

### Property Binding System

Your JavaScript decorators automatically become inspector fields:

```javascript
class PlayerController extends jsBehaviour {
    @Light        // → Shows as "Light" field in inspector
    flashlight;
    
    @Text         // → Shows as "Text" field in inspector
    healthDisplay;
    
    @Rigidbody    // → Shows as "Rigidbody" field in inspector
    playerBody;
}
```

### Inspector Features

- **Auto-Detection**: Properties are automatically found when you assign a JS file
- **Type Safety**: Each property field shows the correct Unity component type
- **Visual Feedback**: Clear indication of which JavaScript property each field represents
- **Add/Remove**: Manually add or remove property bindings as needed
- **Script Analysis**: Live display of detected methods and class information

## 🎮 Example Workflow

1. **Create `PlayerController.js`**:
```javascript
class PlayerController extends jsBehaviour {
    @Rigidbody
    rb;
    
    @Light
    flashlight;
    
    speed = 5.0;
    
    Update() {
        const h = Unity.Input.GetAxis('Horizontal');
        const v = Unity.Input.GetAxis('Vertical');
        
        if (this.rb) {
            this.rb.velocity = new Unity.Vector3(h * this.speed, this.rb.velocity.y, v * this.speed);
        }
        
        if (Unity.Input.GetKeyDown(Unity.KeyCode.F)) {
            this.flashlight.enabled = !this.flashlight.enabled;
        }
    }
}
```

2. **Drag `PlayerController.js`** onto your Player GameObject

3. **In the Inspector**:
   - See "ScriptBehaviour" component added automatically
   - See "Rb" field (Rigidbody type)
   - See "Flashlight" field (Light type)

4. **Assign Components**:
   - Drag your Rigidbody to the "Rb" field
   - Drag your Light to the "Flashlight" field

5. **Play**: Your JavaScript code controls the GameObject!

## 🛠️ Advanced Features

### Multiple Scripts per GameObject

- Add multiple `ScriptBehaviour` components
- Each can have different JavaScript files
- Properties don't conflict between components

### Runtime Script Changes

- Modify JavaScript files while playing
- Changes are reflected immediately (with hot-reload)
- No need to restart play mode

### Property Management

- **Auto-Detect Button**: Automatically find all `@Decorator` properties
- **Manual Add**: Add property bindings for specific JavaScript properties
- **Remove**: Delete unwanted property bindings

### Script Validation

- **Syntax Checking**: Immediate feedback on JavaScript syntax errors
- **Class Validation**: Ensures your class extends `jsBehaviour`
- **Method Detection**: Shows which Unity lifecycle methods you've implemented

## 🎯 Benefits vs. Generated MonoBehaviours

### ✅ This Approach (No Generation)
- **Simple**: Single ScriptBehaviour component handles everything
- **Clean**: No code generation cluttering your project
- **Flexible**: Easy to modify and maintain
- **Predictable**: Always the same component type
- **Version Control Friendly**: No generated files to manage

### ❌ Generated MonoBehaviours (What We Don't Do)
- ❌ Creates new C# files for every JavaScript file
- ❌ Clutters project with generated code
- ❌ Requires compilation step
- ❌ Version control conflicts with generated files
- ❌ Hard to debug generated code

## 🔍 Troubleshooting

### JavaScript File Not Detected
- Ensure file extension is `.js`, `.jsu`, or `.jsfeather`
- Check that class extends `jsBehaviour`
- Verify JavaScript syntax is valid

### Properties Not Showing
- Click "Auto-Detect Properties" button
- Ensure properties have `@Decorator` syntax
- Check JavaScript class has the properties defined

### Runtime Errors
- Check Unity Console for JavaScript execution errors
- Ensure all property fields are assigned in inspector
- Verify Feather Runtime is in the scene

### IntelliSense Not Working
- Ensure TypeScript definitions are generated
- Check `jsconfig.json` exists in `Assets/Feather/Editor/Generated/`
- Restart your IDE if needed

## 🎬 Summary

This system gives you the best of both worlds:
- **Direct JavaScript files** that work like MonoBehaviours
- **No code generation** keeping your project clean
- **Automatic property detection** for rapid development
- **Full Unity integration** with inspector support

Just drag, drop, and code! 🚀
