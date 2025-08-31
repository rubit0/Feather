// Feather-specific TypeScript definitions
// These provide IntelliSense for Feather's JavaScript runtime
// Generated at: 2025-08-31 19:59:52

declare class jsBehaviour {
    gameObject: Unity.GameObject;
    transform: Unity.Transform;

    // Unity lifecycle methods (all optional)
    Awake?(): void;
    Start?(): void;
    OnEnable?(): void;
    OnDisable?(): void;
    Update?(): void;
    LateUpdate?(): void;
    FixedUpdate?(): void;
    OnDestroy?(): void;
}

// Property decorators for Unity object injection
// Usage: @GameObject light; or @Text myText;
declare var GameObject: PropertyDecorator;
declare var Transform: PropertyDecorator;
declare var Rigidbody: PropertyDecorator;
declare var Light: PropertyDecorator;
declare var Camera: PropertyDecorator;
declare var AudioSource: PropertyDecorator;
declare var Text: PropertyDecorator;
declare var Button: PropertyDecorator;
declare var Image: PropertyDecorator;
declare var Slider: PropertyDecorator;
declare var Toggle: PropertyDecorator;

// Global Feather functions
declare function importNamespace(namespace: string): any;

// TypeScript decorator type for property injection
type PropertyDecorator = (target: any, propertyKey: string) => void;
