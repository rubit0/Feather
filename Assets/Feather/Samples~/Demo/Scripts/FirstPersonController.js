class FirstPersonController extends jsBehaviour {
  @Public
  @Header("Look")
  @Tooltip("Transform that pitches with the mouse (usually the Camera)")
  @Required
  cameraPivot = Transform;

  @Public
  @Range(0.1, 10)
  mouseSensitivity = 2.0;

  @Public
  @Range(30, 89)
  maxPitch = 85.0;

  @Public
  @Header("Movement")
  @Range(1, 20)
  walkSpeed = 5.0;

  @Public
  @Range(1, 30)
  sprintSpeed = 9.0;

  @Public
  @Range(0.5, 12)
  jumpHeight = 1.4;

  @Public
  @Range(5, 40)
  gravity = 20.0;

  @Public
  @Header("Cursor")
  @Tooltip("Lock and hide the cursor when play starts")
  lockCursorOnStart = true;

  @Public
  @Header("Audio")
  @Assets
  @Tooltip("Played when jumping")
  jumpSfx = AudioClip;

  Awake() {
    this._controller = this.gameObject.GetComponent(CharacterController);
    this._yaw = this.transform.eulerAngles.y;
    this._pitch = 0;
    this._verticalVelocity = 0;

    if (!this.cameraPivot) {
      const cam = this.gameObject.GetComponentInChildren(Camera);
      if (cam) this.cameraPivot = cam.transform;
    }
  }

  Start() {
    if (!this._controller) {
      Unity.Debug.LogError(
        "[FirstPersonController] Add a CharacterController to this GameObject.",
      );
      return;
    }

    if (this.lockCursorOnStart) this.SetCursorLocked(true);
  }

  OnDisable() {
    this.SetCursorLocked(false);
  }

  Update() {
    if (!this._controller) return;

    const gm = Feather.findBehaviour(GameManager);
    if (gm && gm.won) return;

    if (Unity.Input.GetKeyDown(Unity.KeyCode.Escape)) {
      this.SetCursorLocked(Unity.Cursor.lockState !== Unity.CursorLockMode.Locked);
    }

    this.UpdateLook();
    this.UpdateMove();
  }

  UpdateLook() {
    if (Unity.Cursor.lockState !== Unity.CursorLockMode.Locked) return;

    const sens = this.mouseSensitivity;
    this._yaw += Unity.Input.GetAxis("Mouse X") * sens;
    this._pitch -= Unity.Input.GetAxis("Mouse Y") * sens;

    const max = this.maxPitch;
    if (this._pitch > max) this._pitch = max;
    if (this._pitch < -max) this._pitch = -max;

    this.transform.rotation = Quaternion.Euler(0, this._yaw, 0);
    if (this.cameraPivot) {
      this.cameraPivot.localRotation = Quaternion.Euler(this._pitch, 0, 0);
    }
  }

  UpdateMove() {
    const grounded = this._controller.isGrounded;
    if (grounded && this._verticalVelocity < 0) {
      this._verticalVelocity = -2;
    }

    if (grounded && Unity.Input.GetButtonDown("Jump")) {
      this._verticalVelocity = Math.sqrt(2 * this.gravity * this.jumpHeight);
      if (this.jumpSfx) {
        AudioSource.PlayClipAtPoint(this.jumpSfx, this.transform.position);
      }
    }

    this._verticalVelocity -= this.gravity * Unity.Time.deltaTime;

    const h = Unity.Input.GetAxis("Horizontal");
    const v = Unity.Input.GetAxis("Vertical");
    let input = new Vector3(h, 0, v);
    if (input.sqrMagnitude > 1) input = input.normalized;

    const sprinting = Unity.Input.GetKey(Unity.KeyCode.LeftShift);
    const speed = sprinting ? this.sprintSpeed : this.walkSpeed;
    const world = this.transform.TransformDirection(input);
    const dt = Unity.Time.deltaTime;

    this._controller.Move(
      new Vector3(
        world.x * speed * dt,
        this._verticalVelocity * dt,
        world.z * speed * dt,
      ),
    );
  }

  SetCursorLocked(locked) {
    Unity.Cursor.lockState = locked
      ? Unity.CursorLockMode.Locked
      : Unity.CursorLockMode.None;
    Unity.Cursor.visible = !locked;
  }
}
