# Feather - JavaScript Scripting for Unity

[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-blue)](https://unity3d.com)
[![JavaScript](https://img.shields.io/badge/JavaScript-ES6-yellow)](https://developer.mozilla.org/en-US/docs/Web/JavaScript)
[![Jint](https://img.shields.io/badge/Runtime-Jint-green)](https://github.com/sebastienros/jint)

Feather enables **JavaScript scripting in Unity** with full IntelliSense support, native inspector integration, and MonoBehaviour-like functionality. Write Unity scripts in JavaScript instead of C# while maintaining all the power and features of Unity development.

## ✨ Key Features

- 🎯 **Direct JavaScript Files**: Drop `.js` files onto GameObjects like native MonoBehaviours
- 🔧 **Native Inspector**: Properties show up exactly like C# MonoBehaviour properties
- 💡 **Full IntelliSense**: Complete Unity API auto-completion in your IDE
- 🔄 **Hot Reload**: Script changes reflect immediately in Play Mode
- 📋 **Property Decorators**: `@Light`, `@Button`, `@List(Component)` for clean property injection
- 🎮 **UnityEvent Support**: Full event system integration with inspector callbacks
- 🚀 **IL2CPP Compatible**: Works on mobile platforms (Android/iOS)
- 📦 **AssetBundle Ready**: Deploy scripts dynamically for live updates

## 🚀 Quick Start

### 1. Create a JavaScript Script

```javascript
class HelloWorld extends jsBehaviour {
    @GameObject
    targetObject;
    
    Awake() {
        Unity.Debug.Log('Hello World from JavaScript!');
    }
    
    Start() {
        Unity.Debug.Log('Feather JavaScript is working!');
        
        if (this.targetObject) {
            Unity.Debug.Log(`Found target: ${this.targetObject.name}`);
        }
    }
    
    Update() {
        // Press Space to say hello
        if (Unity.Input.GetKeyDown(Unity.KeyCode.Space)) {
            Unity.Debug.Log('Hello from Update method!');
            
            if (this.targetObject) {
                this.targetObject.name = 'Hello JavaScript!';
            }
        }
    }
}
```

### 2. Add to GameObject

Simply **drag the `.js` file** onto any GameObject in the hierarchy, and:
- ✅ ScriptBehaviour component is automatically added
- ✅ Properties are auto-detected and appear in inspector
- ✅ IntelliSense works in your IDE
- ✅ Ready to assign Unity objects and play!

## 📖 Documentation

- **[Direct JavaScript Guide](./DirectJS_Guide.md)** - Complete guide to drag & drop JavaScript scripting
- **[IntelliSense Setup](./IntelliSense_Guide.md)** - Set up full Unity API auto-completion in your IDE
- **[API Reference](./Assets/Feather/Editor/Generated/)** - Generated TypeScript definitions for Unity APIs

## 🎯 Property System

### Supported Decorators

```javascript
// Unity Components
@GameObject        // GameObject reference
@Transform         // Transform component
@Rigidbody         // Rigidbody component
@Light             // Light component
@Camera            // Camera component
@AudioSource       // AudioSource component

// UI Components
@Text              // UI Text component
@Button            // UI Button component
@Image             // UI Image component
@Slider            // UI Slider component
@Toggle            // UI Toggle component

// Physics
@Collider          // Any Collider component
@BoxCollider       // BoxCollider specifically
@SphereCollider    // SphereCollider specifically
@CapsuleCollider   // CapsuleCollider specifically
@MeshCollider      // MeshCollider specifically

// Events
@UnityEvent        // Unity event system

// Arrays/Lists
@List(Component)   // Arrays of any supported component type
@List(GameObject)  // Arrays of GameObjects
@List(UnityEvent)  // Arrays of UnityEvents
```

### Inspector Integration

All properties appear in the Unity Inspector with:
- ✅ **Correct component types** (Button field shows Button components only)
- ✅ **Native drag & drop** (same UX as C# MonoBehaviours)
- ✅ **List/Array support** with size controls and reorderable elements
- ✅ **UnityEvent callbacks** with method selection dropdowns
- ✅ **Auto-detection** when scripts change

## 🛠️ Architecture

### Core Components

- **`Runtime.cs`**: Jint JavaScript engine manager with hot-reload support
- **`ScriptBehaviour.cs`**: MonoBehaviour host that executes JavaScript classes
- **`Analyzer.cs`**: JavaScript parser for property and method detection
- **`ScriptBehaviourEditor.cs`**: Custom inspector with native MonoBehaviour UI
- **`ImportHandler.cs`**: Asset import pipeline for `.js`/`.jsu`/`.jsfeather` files

### JavaScript Engine

- **Runtime**: [Jint .NET JavaScript Engine](https://github.com/sebastienros/jint)
- **JavaScript Version**: ES6 (classes, arrow functions, destructuring, etc.)
- **Unity API**: Available through `Unity.*` namespace
- **Performance**: Compiled JavaScript with direct .NET object binding

## 🔧 Installation

1. **Import Feather** into your Unity project
2. **Initialize Runtime**: 
   - Add `Feather Runtime` to your scene, or
   - Use menu `GameObject > Feather > Create Runtime Manager`
3. **Generate IntelliSense**: 
   - Menu `Feather > Generate TypeScript Definitions`
   - Configure your IDE to recognize `.js` files

## 📋 Requirements

- **Unity**: 2021.3 or later
- **IL2CPP Compatible**: Works on all Unity platforms
- **IDE Support**: Any editor with TypeScript/JavaScript support (VS Code recommended)

## 🎯 Use Cases

### ✅ Perfect For
- **Rapid Prototyping**: Faster iteration than C# compilation
- **Dynamic Content**: Scripts that can be updated via AssetBundles
- **Team Workflows**: Designers/scripters familiar with JavaScript
- **Live Updates**: Hot-reload development without stopping play mode
- **Cross-Platform**: Same scripts work on all Unity-supported platforms

### ⚠️ Consider C# For
- **Performance-Critical Code**: Jint has overhead compared to native C#
- **Complex Math Operations**: Vector math intensive applications
- **Large Codebases**: C# provides better tooling for massive projects

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **[Jint](https://github.com/sebastienros/jint)** - JavaScript engine for .NET
- **Unity Technologies** - For the amazing Unity engine
- **Community** - For feedback and contributions

---

**Get started today!** Create a `.js` file, drag it onto a GameObject, and experience the power of JavaScript in Unity! 🚀