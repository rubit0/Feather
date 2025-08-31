# Feather JavaScript IntelliSense Setup

This guide explains how to get full IntelliSense support for Feather JavaScript scripts in your IDE.

## What You Get

- **Full Unity API IntelliSense**: Auto-completion for Unity classes, methods, properties, and enums
- **Feather-Specific Support**: IntelliSense for `jsBehaviour` base class and property decorators
- **Error Detection**: Basic syntax error detection in your JavaScript
- **Type Hints**: Hover information for Unity types and methods

## Automatic Setup

The TypeScript definitions are generated automatically when:
1. Unity starts up (if definitions don't exist)
2. You use the menu `Feather > Generate TypeScript Definitions`
3. You import a new Feather script file

## IDE Configuration

### Visual Studio Code

1. Open your project folder in VS Code
2. The `jsconfig.json` file in `Assets/Feather/Editor/Generated/` will be automatically detected
3. Your `.txt` script files will be treated as JavaScript
4. IntelliSense will work immediately

### WebStorm/IntelliJ

1. Open your project in WebStorm
2. Go to Settings > Languages & Frameworks > JavaScript
3. Set JavaScript language version to ES6
4. Point to the `jsconfig.json` file in `Assets/Feather/Editor/Generated/`

### Other IDEs

Any IDE that supports TypeScript definitions should work:
1. Ensure it recognizes `.txt` files as JavaScript
2. Point it to the generated `*.d.ts` files in `Assets/Feather/Editor/Generated/`

## Script Writing

### ES6 JavaScript Features Supported by Jint

✅ **Classes**: `class MyScript extends jsBehaviour`  
✅ **Arrow Functions**: `() => { this.doSomething(); }`  
✅ **Let/Const**: `let x = 5; const PI = 3.14;`  
✅ **Template Literals**: `` `Hello ${this.gameObject.name}` ``  
✅ **Destructuring**: `const {x, y} = this.transform.position;`  
✅ **Default Parameters**: `function move(speed = 1.0) { ... }`  

### Feather-Specific Features

#### Property Decorators
```javascript
class MyScript extends jsBehaviour {
    @Light
    myLight;        // Will be injected by Feather
    
    @Text
    statusText;     // Will be injected by Feather
}
```

#### Unity Lifecycle Methods
```javascript
class MyScript extends jsBehaviour {
    Awake() { }         // Called when object is created
    Start() { }         // Called before first frame
    Update() { }        // Called every frame
    OnDestroy() { }     // Called when object is destroyed
}
```

#### Unity API Access
```javascript
// All Unity APIs are available through the Unity namespace
Unity.Debug.Log('Hello World');
Unity.Input.GetKeyDown(Unity.KeyCode.Space);
this.transform.position = new Unity.Vector3(0, 5, 0);
```

## Available Unity Types in IntelliSense

The generated definitions include commonly used Unity types:

- **Core**: GameObject, Transform, Component, MonoBehaviour
- **Physics**: Rigidbody, Collider, ForceMode
- **Rendering**: Renderer, Light, Camera, Color
- **Input**: Input, KeyCode
- **Math**: Vector2, Vector3, Vector4, Quaternion
- **UI**: Canvas, Text, Button, Image, Slider
- **Audio**: AudioSource, AudioClip
- **Utilities**: Debug, Time, Random

## Troubleshooting

### IntelliSense Not Working

1. Check that TypeScript definitions exist in `Assets/Feather/Editor/Generated/`
2. Run `Feather > Generate TypeScript Definitions` from Unity menu
3. Restart your IDE
4. Ensure your IDE recognizes `.txt` files as JavaScript

### Missing Unity Types

If you need additional Unity types:
1. Add them to the `IsCommonUnityType()` method in `TypeScriptDefinitionGenerator.cs`
2. Regenerate definitions using the Unity menu

### Performance Issues

The generated definitions are filtered to include only commonly used members. If you need more complete definitions, you can modify the `IsCommonMember()` method in the generator.

## File Extensions

- **Script Files**: Use `.txt` extension (Unity TextAsset requirement)
- **Alternative**: Use `.jsfeather` or `.jsu` extensions (handled by ImportHandler)
- **IDE Recognition**: All extensions are configured to be treated as JavaScript

## Best Practices

1. **Use TypeScript definitions for IntelliSense only** - Your actual code runs as ES6 JavaScript in Jint
2. **Follow ES6 syntax** - Take advantage of classes, arrow functions, etc.
3. **Use Unity namespace** - Always prefix Unity APIs with `Unity.`
4. **Leverage decorators** - Use `@ComponentType` for clean property injection
5. **Test frequently** - Jint runtime behavior may differ slightly from browser JavaScript
