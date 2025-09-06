// Auto-generated Unity TypeScript definitions for Feather
// These are for IntelliSense only - actual code runs as ES6 JavaScript in Jint
// Generated at: 2025-09-06 13:27:37

declare namespace Unity {
    class Behaviour extends Component {
        enabled: boolean;
        readonly isActiveAndEnabled: boolean;
        readonly transform: Unity.Transform;
        readonly gameObject: Unity.GameObject;
        tag: string;
        name: string;
    }

    class Camera extends Behaviour {
        readonly velocity: Unity.Vector3;
        enabled: boolean;
        readonly isActiveAndEnabled: boolean;
        readonly transform: Unity.Transform;
        readonly gameObject: Unity.GameObject;
        tag: string;
        name: string;
    }

    interface Color {
    }

    class Component {
        readonly transform: Unity.Transform;
        readonly gameObject: Unity.GameObject;
        tag: string;
        name: string;
        GetComponent(type: any): Unity.Component;
        GetComponentInChildren(t: any, includeInactive: boolean): Unity.Component;
        GetComponentInParent(t: any, includeInactive: boolean): Unity.Component;
        CompareTag(tag: string): boolean;
    }

    class Debug {
        static Log(message: any): void;
        static LogError(message: any): void;
        static LogWarning(message: any): void;
    }

    class GameObject {
        readonly transform: Unity.Transform;
        layer: number;
        active: boolean;
        tag: string;
        readonly gameObject: Unity.GameObject;
        name: string;
        GetComponent(): Unity.T;
        GetComponentInChildren(type: any, includeInactive: boolean): Unity.Component;
        GetComponentInParent(type: any, includeInactive: boolean): Unity.Component;
        AddComponent(componentType: any): Unity.Component;
        SetActive(value: boolean): void;
        CompareTag(tag: string): boolean;
    }

    namespace Input {
    }

    enum KeyCode {
        None = 0,
        Backspace = 8,
        Tab = 9,
        Clear = 12,
        Return = 13,
        Pause = 19,
        Escape = 27,
        Space = 32,
        Exclaim = 33,
        DoubleQuote = 34,
        Hash = 35,
        Dollar = 36,
        Percent = 37,
        Ampersand = 38,
        Quote = 39,
        LeftParen = 40,
        RightParen = 41,
        Asterisk = 42,
        Plus = 43,
        Comma = 44,
        Minus = 45,
        Period = 46,
        Slash = 47,
        Alpha0 = 48,
        Alpha1 = 49,
        Alpha2 = 50,
        Alpha3 = 51,
        Alpha4 = 52,
        Alpha5 = 53,
        Alpha6 = 54,
        Alpha7 = 55,
        Alpha8 = 56,
        Alpha9 = 57,
        Colon = 58,
        Semicolon = 59,
        Less = 60,
        Equals = 61,
        Greater = 62,
        Question = 63,
        At = 64,
        LeftBracket = 91,
        Backslash = 92,
        RightBracket = 93,
        Caret = 94,
        Underscore = 95,
        BackQuote = 96,
        A = 97,
        B = 98,
        C = 99,
        D = 100,
        E = 101,
        F = 102,
        G = 103,
        H = 104,
        I = 105,
        J = 106,
        K = 107,
        L = 108,
        M = 109,
        N = 110,
        O = 111,
        P = 112,
        Q = 113,
        R = 114,
        S = 115,
        T = 116,
        U = 117,
        V = 118,
        W = 119,
        X = 120,
        Y = 121,
        Z = 122,
        LeftCurlyBracket = 123,
        Pipe = 124,
        RightCurlyBracket = 125,
        Tilde = 126,
        Delete = 127,
        Keypad0 = 256,
        Keypad1 = 257,
        Keypad2 = 258,
        Keypad3 = 259,
        Keypad4 = 260,
        Keypad5 = 261,
        Keypad6 = 262,
        Keypad7 = 263,
        Keypad8 = 264,
        Keypad9 = 265,
        KeypadPeriod = 266,
        KeypadDivide = 267,
        KeypadMultiply = 268,
        KeypadMinus = 269,
        KeypadPlus = 270,
        KeypadEnter = 271,
        KeypadEquals = 272,
        UpArrow = 273,
        DownArrow = 274,
        RightArrow = 275,
        LeftArrow = 276,
        Insert = 277,
        Home = 278,
        End = 279,
        PageUp = 280,
        PageDown = 281,
        F1 = 282,
        F2 = 283,
        F3 = 284,
        F4 = 285,
        F5 = 286,
        F6 = 287,
        F7 = 288,
        F8 = 289,
        F9 = 290,
        F10 = 291,
        F11 = 292,
        F12 = 293,
        F13 = 294,
        F14 = 295,
        F15 = 296,
        Numlock = 300,
        CapsLock = 301,
        ScrollLock = 302,
        RightShift = 303,
        LeftShift = 304,
        RightControl = 305,
        LeftControl = 306,
        RightAlt = 307,
        LeftAlt = 308,
        RightCommand = 309,
        RightMeta = 309,
        RightApple = 309,
        LeftCommand = 310,
        LeftMeta = 310,
        LeftApple = 310,
        LeftWindows = 311,
        RightWindows = 312,
        AltGr = 313,
        Help = 315,
        Print = 316,
        SysReq = 317,
        Break = 318,
        Menu = 319,
        WheelUp = 321,
        WheelDown = 322,
        Mouse0 = 323,
        Mouse1 = 324,
        Mouse2 = 325,
        Mouse3 = 326,
        Mouse4 = 327,
        Mouse5 = 328,
        Mouse6 = 329,
        JoystickButton0 = 330,
        JoystickButton1 = 331,
        JoystickButton2 = 332,
        JoystickButton3 = 333,
        JoystickButton4 = 334,
        JoystickButton5 = 335,
        JoystickButton6 = 336,
        JoystickButton7 = 337,
        JoystickButton8 = 338,
        JoystickButton9 = 339,
        JoystickButton10 = 340,
        JoystickButton11 = 341,
        JoystickButton12 = 342,
        JoystickButton13 = 343,
        JoystickButton14 = 344,
        JoystickButton15 = 345,
        JoystickButton16 = 346,
        JoystickButton17 = 347,
        JoystickButton18 = 348,
        JoystickButton19 = 349,
        Joystick1Button0 = 350,
        Joystick1Button1 = 351,
        Joystick1Button2 = 352,
        Joystick1Button3 = 353,
        Joystick1Button4 = 354,
        Joystick1Button5 = 355,
        Joystick1Button6 = 356,
        Joystick1Button7 = 357,
        Joystick1Button8 = 358,
        Joystick1Button9 = 359,
        Joystick1Button10 = 360,
        Joystick1Button11 = 361,
        Joystick1Button12 = 362,
        Joystick1Button13 = 363,
        Joystick1Button14 = 364,
        Joystick1Button15 = 365,
        Joystick1Button16 = 366,
        Joystick1Button17 = 367,
        Joystick1Button18 = 368,
        Joystick1Button19 = 369,
        Joystick2Button0 = 370,
        Joystick2Button1 = 371,
        Joystick2Button2 = 372,
        Joystick2Button3 = 373,
        Joystick2Button4 = 374,
        Joystick2Button5 = 375,
        Joystick2Button6 = 376,
        Joystick2Button7 = 377,
        Joystick2Button8 = 378,
        Joystick2Button9 = 379,
        Joystick2Button10 = 380,
        Joystick2Button11 = 381,
        Joystick2Button12 = 382,
        Joystick2Button13 = 383,
        Joystick2Button14 = 384,
        Joystick2Button15 = 385,
        Joystick2Button16 = 386,
        Joystick2Button17 = 387,
        Joystick2Button18 = 388,
        Joystick2Button19 = 389,
        Joystick3Button0 = 390,
        Joystick3Button1 = 391,
        Joystick3Button2 = 392,
        Joystick3Button3 = 393,
        Joystick3Button4 = 394,
        Joystick3Button5 = 395,
        Joystick3Button6 = 396,
        Joystick3Button7 = 397,
        Joystick3Button8 = 398,
        Joystick3Button9 = 399,
        Joystick3Button10 = 400,
        Joystick3Button11 = 401,
        Joystick3Button12 = 402,
        Joystick3Button13 = 403,
        Joystick3Button14 = 404,
        Joystick3Button15 = 405,
        Joystick3Button16 = 406,
        Joystick3Button17 = 407,
        Joystick3Button18 = 408,
        Joystick3Button19 = 409,
        Joystick4Button0 = 410,
        Joystick4Button1 = 411,
        Joystick4Button2 = 412,
        Joystick4Button3 = 413,
        Joystick4Button4 = 414,
        Joystick4Button5 = 415,
        Joystick4Button6 = 416,
        Joystick4Button7 = 417,
        Joystick4Button8 = 418,
        Joystick4Button9 = 419,
        Joystick4Button10 = 420,
        Joystick4Button11 = 421,
        Joystick4Button12 = 422,
        Joystick4Button13 = 423,
        Joystick4Button14 = 424,
        Joystick4Button15 = 425,
        Joystick4Button16 = 426,
        Joystick4Button17 = 427,
        Joystick4Button18 = 428,
        Joystick4Button19 = 429,
        Joystick5Button0 = 430,
        Joystick5Button1 = 431,
        Joystick5Button2 = 432,
        Joystick5Button3 = 433,
        Joystick5Button4 = 434,
        Joystick5Button5 = 435,
        Joystick5Button6 = 436,
        Joystick5Button7 = 437,
        Joystick5Button8 = 438,
        Joystick5Button9 = 439,
        Joystick5Button10 = 440,
        Joystick5Button11 = 441,
        Joystick5Button12 = 442,
        Joystick5Button13 = 443,
        Joystick5Button14 = 444,
        Joystick5Button15 = 445,
        Joystick5Button16 = 446,
        Joystick5Button17 = 447,
        Joystick5Button18 = 448,
        Joystick5Button19 = 449,
        Joystick6Button0 = 450,
        Joystick6Button1 = 451,
        Joystick6Button2 = 452,
        Joystick6Button3 = 453,
        Joystick6Button4 = 454,
        Joystick6Button5 = 455,
        Joystick6Button6 = 456,
        Joystick6Button7 = 457,
        Joystick6Button8 = 458,
        Joystick6Button9 = 459,
        Joystick6Button10 = 460,
        Joystick6Button11 = 461,
        Joystick6Button12 = 462,
        Joystick6Button13 = 463,
        Joystick6Button14 = 464,
        Joystick6Button15 = 465,
        Joystick6Button16 = 466,
        Joystick6Button17 = 467,
        Joystick6Button18 = 468,
        Joystick6Button19 = 469,
        Joystick7Button0 = 470,
        Joystick7Button1 = 471,
        Joystick7Button2 = 472,
        Joystick7Button3 = 473,
        Joystick7Button4 = 474,
        Joystick7Button5 = 475,
        Joystick7Button6 = 476,
        Joystick7Button7 = 477,
        Joystick7Button8 = 478,
        Joystick7Button9 = 479,
        Joystick7Button10 = 480,
        Joystick7Button11 = 481,
        Joystick7Button12 = 482,
        Joystick7Button13 = 483,
        Joystick7Button14 = 484,
        Joystick7Button15 = 485,
        Joystick7Button16 = 486,
        Joystick7Button17 = 487,
        Joystick7Button18 = 488,
        Joystick7Button19 = 489,
        Joystick8Button0 = 490,
        Joystick8Button1 = 491,
        Joystick8Button2 = 492,
        Joystick8Button3 = 493,
        Joystick8Button4 = 494,
        Joystick8Button5 = 495,
        Joystick8Button6 = 496,
        Joystick8Button7 = 497,
        Joystick8Button8 = 498,
        Joystick8Button9 = 499,
        Joystick8Button10 = 500,
        Joystick8Button11 = 501,
        Joystick8Button12 = 502,
        Joystick8Button13 = 503,
        Joystick8Button14 = 504,
        Joystick8Button15 = 505,
        Joystick8Button16 = 506,
        Joystick8Button17 = 507,
        Joystick8Button18 = 508,
        Joystick8Button19 = 509,
        F16 = 670,
        F17 = 671,
        F18 = 672,
        F19 = 673,
        F20 = 674,
        F21 = 675,
        F22 = 676,
        F23 = 677,
        F24 = 678,
    }

    class Light extends Behaviour {
        color: Unity.Color;
        enabled: boolean;
        readonly isActiveAndEnabled: boolean;
        readonly transform: Unity.Transform;
        readonly gameObject: Unity.GameObject;
        tag: string;
        name: string;
    }

    class MeshRenderer extends Renderer {
        bounds: Unity.Bounds;
        enabled: boolean;
        materials: Unity.Material[];
        material: Unity.Material;
        readonly transform: Unity.Transform;
        readonly gameObject: Unity.GameObject;
        tag: string;
        name: string;
    }

    class MonoBehaviour extends Behaviour {
        enabled: boolean;
        readonly isActiveAndEnabled: boolean;
        readonly transform: Unity.Transform;
        readonly gameObject: Unity.GameObject;
        tag: string;
        name: string;
    }

    interface Quaternion {
    }

    namespace Random {
        static readonly rotation: Unity.Quaternion;
    }

    interface Rect {
        position: Unity.Vector2;
    }

    class Renderer extends Component {
        bounds: Unity.Bounds;
        enabled: boolean;
        materials: Unity.Material[];
        material: Unity.Material;
        readonly transform: Unity.Transform;
        readonly gameObject: Unity.GameObject;
        tag: string;
        name: string;
    }

    enum Space {
        World = 0,
        Self = 1,
    }

    class Time {
        static readonly time: number;
        static readonly deltaTime: number;
        static fixedDeltaTime: number;
        static timeScale: number;
    }

    class Transform extends Component {
        position: Unity.Vector3;
        localPosition: Unity.Vector3;
        rotation: Unity.Quaternion;
        localRotation: Unity.Quaternion;
        localScale: Unity.Vector3;
        readonly transform: Unity.Transform;
        readonly gameObject: Unity.GameObject;
        tag: string;
        name: string;
        Translate(translation: Unity.Vector3, relativeTo: Unity.Space): void;
        Rotate(eulers: Unity.Vector3, relativeTo: Unity.Space): void;
        LookAt(target: Unity.Transform, worldUp: Unity.Vector3): void;
    }

    interface Vector2 {
    }

    interface Vector3 {
    }

    interface Vector4 {
    }

    class Collider extends Component {
        enabled: boolean;
        readonly bounds: Unity.Bounds;
        material: Unity.PhysicsMaterial;
        readonly transform: Unity.Transform;
        readonly gameObject: Unity.GameObject;
        tag: string;
        name: string;
    }

    enum ForceMode {
        Force = 0,
        Impulse = 1,
        VelocityChange = 2,
        Acceleration = 5,
    }

    class Rigidbody extends Component {
        angularVelocity: Unity.Vector3;
        mass: number;
        useGravity: boolean;
        isKinematic: boolean;
        position: Unity.Vector3;
        rotation: Unity.Quaternion;
        velocity: Unity.Vector3;
        readonly transform: Unity.Transform;
        readonly gameObject: Unity.GameObject;
        tag: string;
        AddForce(force: Unity.Vector3, mode: Unity.ForceMode): void;
        AddTorque(torque: Unity.Vector3, mode: Unity.ForceMode): void;
    }

    class AudioClip {
        name: string;
    }

    class AudioSource {
        time: number;
        enabled: boolean;
        readonly isActiveAndEnabled: boolean;
        readonly transform: Unity.Transform;
        readonly gameObject: Unity.GameObject;
        tag: string;
        name: string;
    }

    class Input {
        static readonly mousePosition: Unity.Vector3;
        static GetAxis(axisName: string): number;
        static GetAxisRaw(axisName: string): number;
        static GetButton(buttonName: string): boolean;
        static GetButtonDown(buttonName: string): boolean;
        static GetButtonUp(buttonName: string): boolean;
        static GetKey(key: Unity.KeyCode): boolean;
        static GetKeyUp(key: Unity.KeyCode): boolean;
        static GetKeyDown(key: Unity.KeyCode): boolean;
    }

    class Canvas extends Behaviour {
        enabled: boolean;
        readonly isActiveAndEnabled: boolean;
        readonly transform: Unity.Transform;
        readonly gameObject: Unity.GameObject;
        tag: string;
        name: string;
    }

}

// Global Unity namespace (available via importNamespace('UnityEngine'))
declare var Unity: typeof Unity;
