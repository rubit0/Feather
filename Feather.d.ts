// Feather-specific TypeScript definitions
// These provide IntelliSense for Feather's JavaScript runtime
// Generated at: 2025-09-06 14:44:08

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

    // Physics 3D methods
    OnCollisionEnter?(collision: Unity.Collision): void;
    OnCollisionStay?(collision: Unity.Collision): void;
    OnCollisionExit?(collision: Unity.Collision): void;
    OnTriggerEnter?(other: Unity.Collider): void;
    OnTriggerStay?(other: Unity.Collider): void;
    OnTriggerExit?(other: Unity.Collider): void;

    // Physics 2D methods
    OnCollisionEnter2D?(collision: Unity.Collision2D): void;
    OnCollisionStay2D?(collision: Unity.Collision2D): void;
    OnCollisionExit2D?(collision: Unity.Collision2D): void;
    OnTriggerEnter2D?(other: Unity.Collider2D): void;
    OnTriggerStay2D?(other: Unity.Collider2D): void;
    OnTriggerExit2D?(other: Unity.Collider2D): void;

    // Rendering methods
    OnBecameVisible?(): void;
    OnBecameInvisible?(): void;
    OnWillRenderObject?(): void;
    OnRenderObject?(): void;

    // Application methods
    OnApplicationFocus?(hasFocus: boolean): void;
    OnApplicationPause?(pauseStatus: boolean): void;
    OnApplicationQuit?(): void;

    // GUI & Gizmo methods
    OnGUI?(): void;
    OnDrawGizmos?(): void;
    OnDrawGizmosSelected?(): void;

    // Animation methods
    OnAnimatorIK?(layerIndex: number): void;
    OnAnimatorMove?(): void;
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
