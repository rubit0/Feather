class scriptTest extends jsBehaviour {
    @Light
    light;
    @Text
    text;
    @Button
    button;
    
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