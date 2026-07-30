# Feather - JavaScript Scripting for Unity

[![Unity](https://img.shields.io/badge/Unity-6000.5%2B-blue)](https://unity3d.com)
[![JavaScript](https://img.shields.io/badge/JavaScript-ES6-yellow)](https://developer.mozilla.org/en-US/docs/Web/JavaScript)
[![Jint](https://img.shields.io/badge/Runtime-Jint-green)](https://github.com/sebastienros/jint)

Feather enables **JavaScript scripting in Unity** with IntelliSense, inspector fields like MonoBehaviours, and drag-and-drop `.js` components. No manual setup — add the UPM package, create a script, attach it.

## Install (UPM)

**Git URL** (Window → Package Manager → **+** → **Add package from git URL…**):

```
https://github.com/rubit0/Feather.git?path=/Assets/Feather
```

Or in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.rubit0.feather": "https://github.com/rubit0/Feather.git?path=/Assets/Feather"
  }
}
```

Optional: pin a tag/branch with `#v1.0.0` or `#main` after the path (e.g. `...?path=/Assets/Feather#main`).

This repository is a Unity project used to develop Feather. The installable package is [`Assets/Feather`](Assets/Feather) (contains `package.json`); it is **not** under `Packages/`. The demo sample is `Assets/Feather/Samples~/Demo` — import it from Package Manager → Feather → Samples.

## Quick Start

1. **Assets → Create → JavaScript Behaviour**
2. Drag the `.js` onto a GameObject (or **Component → Feather → Add JavaScript…**)
3. Press Play

`RuntimeStarter` creates a persistent `FeatherRuntime` before the first scene loads. IntelliSense (`jsconfig.json`, `Unity*.d.ts`, `Feather.d.ts`) is generated automatically on first load.

```javascript
class HelloWorld extends jsBehaviour {
    @Public
    targetObject = GameObject;

    @Public
    speed = 5.0;

    Start() {
        Unity.Debug.Log('Hello from JavaScript!');
    }

    Update() {
        if (Unity.Input.GetKeyDown(Unity.KeyCode.Space)) {
            Unity.Debug.Log(`speed=${this.speed}`);
        }
    }
}
```

Prefer **class name == file name** (same as C#).

## Key Features

- **Direct `.js` components** — drag onto GameObjects like scripts
- **Inspector fields** — `@Public` + typed markers (`= MeshRenderer`, `= Coin`, `List(GameObject)`)
- **IntelliSense** — auto-generated; regenerate in **Project Settings → Feather**
- **Hot reload** — script changes recreate the JS engine in Play Mode (editor)
- **Generator coroutines** — `this.startCoroutine(this.myGenerator())` with `yield null` / seconds
- **Helpers** — `invoke`, `invokeRepeating`, `startCoroutine`, `this.wait` / `this.nextFrame`, `Feather.require`, `Feather.findBehaviour(s)`
- **Player builds** — scripts collected automatically on build
- **Runtime registration** — load scripts from AssetBundles, memory, or a URL (see below)

## Property System

Only `@Public` fields appear in the Inspector.

```javascript
@Public @Header("Refs") @Required targetObject = GameObject;
@Public @Assets texture = Texture2D;
@Public @Scene follow = Transform;
@Public @Range(0, 20) speed = 5.0;
@Public tint = Color;
@Public offset = Vector3;
@Public enemies = List(GameObject);
@Public otherScript = Coin;           // JS class ref → peer JS instance at runtime
@Public onDone = UnityEvent;
```

**JS behaviour refs:** `@Public other = Coin` — Inspector slot filtered to that class; runtime injects the peer JS instance.

**Operators:** JS has no C# overloads — use `Color.multiply(a, b)`, `Vector3.add(a, b)`, etc.

**Finding objects:**

```javascript
const light = Unity.Object.FindObjectOfType(Unity.Light);
const other = Feather.findBehaviour(Coin);
const all = Feather.findBehaviours(Coin);
const inactive = Feather.findBehaviours(Coin, { includeInactive: true });
const fromGo = Feather.getBehaviour(someGameObject, Coin);
const inScene = Feather.findBehavioursInScene("DemoScene", Coin);
```

**Coroutines / yields:**

```javascript
*fade() {
  yield this.wait(0.5);
  yield this.nextFrame();
  yield Feather.waitUntil(() => this.ready);
}
this.startCoroutine(this.fade());
```

## Runtime script loading

Scripts in the project are loaded at startup. For **AssetBundles** or **backend-delivered** source, register before activating prefabs/scenes that use those classes.

**C#**

```csharp
Runtime.Instance.RegisterScript(source, "MyClass");
Runtime.Instance.RegisterScript(jsAsset);
Runtime.Instance.RegisterScriptsFromBundle(bundle);
Runtime.Instance.LoadBundleFromFile(path); // LoadFromFile + register; returns the open bundle
Runtime.Instance.RegisterScript(newSource, "MyClass", replace: true);
```

**JavaScript** (same API on `Feather`)

```javascript
Feather.registerScript(source, "MyClass");
Feather.registerScript(jsAsset);
Feather.registerScriptsFromBundle(bundle);
Feather.registerScript(newSource, "MyClass", true);

// LoadFromFile + register scripts in one step (bundle stays open for prefabs)
const bundle = Feather.loadBundleFromFile(path);
// … Instantiate prefabs from bundle …
bundle.Unload(false);

// Or from downloaded bytes / URL
Feather.loadBundleFromMemory(bytes);
Feather.downloadAndRegister(url, (className, error) => {
  if (error) console.log(error);
  else Feather.createBehaviour(go, className);
});

if (!Feather.isScriptLoaded(MyClass)) {
  Feather.loadBundleFromFile(path);
}

Feather.listScripts();           // registered class names
Feather.getScript(MyClass);      // class ctor
Feather.unloadScript(MyClass);   // session unload (re-register to restore)
Feather.reloadAll();             // recreate all JS hosts
Feather.onSceneLoaded((scene, mode) => { /* … */ });
```

Registered scripts persist across engine rebuilds for the session. Prefabs must already reference the `JavaScript` asset (or use hosts wired in the bundle). Inspector bridge fields are baked at edit time — new classes discovered only at runtime need prefabs built with matching bridge data.

## Settings

**Edit → Project Settings → Feather**

| Control | Purpose |
|---------|---------|
| **Generate / Update JS Project** | Refresh `Unity*.d.ts`, `Feather.d.ts`, `Project.d.ts`, `jsconfig.json`, `link.xml` |
| **Open .js files with** | Auto / Cursor / VS Code / Unity default / custom |
| **API Packages** | Opt into UPM packages for JS types / AllowClr |
| **Allow System.Reflection** | Off by default |
| Logging | Verbose / script load / component add |

## Architecture

| Piece | Role |
|-------|------|
| `JavaScriptBehaviour` | MonoBehaviour host + serialized bridge fields |
| `Runtime` | Jint engine, script load, require, hot reload, `RegisterScript` |
| `UnityApiSurface` | Shared AllowClr assemblies (runtime + `.d.ts` + link.xml) |
| `Analyzer` | Esprima parse of class fields/methods |
| `JavaScriptImporter` | `.js` → `JavaScript` asset |

## Requirements

- Unity 6 (project targets 6000.5)
- IDE with TypeScript/JavaScript support (Cursor / VS Code recommended)

## Honest limits

- Jint has overhead vs native C#; avoid heavy per-frame math in JS when possible
- Generics / `ref` / `out` / extension methods are weak via CLR interop
- Remote JS is arbitrary code — trust/sign your update channel
- Optional packages (Input System, Cinemachine, URP) via **API Packages** in settings

## License

MIT — see [LICENSE](LICENSE)
