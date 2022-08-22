class scriptTest extends jsBehaviour {
    @Light
    light;
    @Text
    text;
    @Button
    button;

    onStart() {
        // Test if this method was called
        unity.Debug.Log('Called [onStart]');

        // Test if setting strings work
        this.gameObject.name = 'Mr Jint';
        this.text.text = 'Hello from JavaScript';
        
        // Test if setting floats work
        this.light.intensity = 0;
        
        // Test if assigning lambdas to unity callbacks workds
        this.button.onClick.AddListener(() => {
            if(this.light.intensity > 0.01){
                this.light.intensity = 0;
                this.text.text = 'Light is off';
            }else{
                this.light.intensity = 1.0;
                this.text.text = 'Light is on';
            }
        });
    }
}