class scriptTest extends jsBehaviour {
    @Light
    light;
    @Text
    text;
    @Button
    button;
    @List(GameObject)
    gameObjectsIterator;
    @UnityEvent
    onInitCompleted;
    
    Awake(){
        Unity.Debug.Log('Called [Awake]');
    }

    Start(){
        Unity.Debug.Log('Called [Start]');
    }

    OnEnable(){
        Unity.Debug.Log('Called [OnEnable]');
        this.Init();
    }

    OnDisable(){
        Unity.Debug.Log('Called [OnDisable]');
    }

    Update(){
        if(Unity.Input.GetKeyDown (Unity.KeyCode.Space)){
            this.ToggleLight();
        }
    }

    OnDestroy(){
        Unity.Debug.Log('Called [OnDestroy]');
    }

    Init() {
        // Test setting strings
        this.gameObject.name = 'Mr Jint';
        this.text.text = 'Hello from JavaScript';
        
        // Test setting floats
        this.light.intensity = 0;
        
        // Test lambdas on unity callbacks
        this.button.onClick.AddListener(() => {
            this.ToggleLight();
        });

        // Test button arrays - exactly like native MonoBehaviour!
        if (this.gameObjectsIterator && this.gameObjectsIterator.length > 0) {
            Unity.Debug.Log(`Found ${this.gameObjectsIterator.length} gameObjects in array`);
            
            for (let i = 0; i < this.gameObjectsIterator.length; i++) {
                let gameObject = this.gameObjectsIterator[i];
                Unity.Debug.Log(`Go in array: ${this.gameObjectsIterator[i].name}`);
            }
        }
        
        // Test UnityEvent functionality
        if (this.onInitCompleted) {
            Unity.Debug.Log('UnityEvent found! Invoking onInitCompleted...');
            this.onInitCompleted.Invoke();
        } else {
            Unity.Debug.Log('UnityEvent not found or not assigned');
        }
    }
    
    OnArrayButtonClick(index) {
        this.text.text = `Button ${index} was clicked!`;
        Unity.Debug.Log(`Handling click for button ${index}`);
    }
    
    OnInitCompletedCallback() {
        Unity.Debug.Log('🎉 UnityEvent callback triggered! Init was completed successfully!');
        this.text.text = 'Init completed via UnityEvent!';
    }
    
    ToggleLight(){
        if(this.light.intensity > 0.01){
            this.light.intensity = 0;
            this.text.text = 'Light is off';
        }else{
            this.light.intensity = 1.0;
            this.text.text = 'Light is on';
        }
    }
}