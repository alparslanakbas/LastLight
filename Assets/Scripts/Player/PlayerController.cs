using LastLight.Audio;
using LastLight.Settings;
using LastLight.Skills;
using LastLight.UI;
using LastLight.Voxel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.Player
{
    /// <summary>
    /// Birinci sahis hareket ve kamera. Input System'in dogrudan cihaz API'si
    /// kullaniliyor (Keyboard.current / Mouse.current); .inputactions varligi
    /// kurmaya gerek kalmiyor - prototip asamasinda o kurulum fazladan bakim isi.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Hareket")]
        [SerializeField] float walkSpeed = 5f;
        [SerializeField] float sprintSpeed = 8.5f;
        [SerializeField] float jumpHeight = 1.2f;
        [SerializeField] float gravity = -22f;   // gercekci -9.81 yerine daha "oyunsu" bir dusus

        [Header("Kamera")]
        [SerializeField] Transform cameraPivot;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float maxPitch = 89f;

        CharacterController _controller;
        float _pitch;
        float _verticalVelocity;

        // Adim sesi zamanla degil KAT EDILEN YOL ile tetikleniyor: zamana
        // baglasak kosarken ve yururken ayni ritim cikiyor ve hiz hissi
        // kayboluyordu.
        VoxelWorld _world;
        float _stepDistance;
        const float StepStride = 2.2f;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            if (Keyboard.current == null || Mouse.current == null) return;

            // ESC ve imlec kilidi artik duraklama menusunun isi; burada da
            // ele alirsak iki sistem ayni tusa tepki verip menuyu ayni karede
            // acip kapatiyordu.
            if (PauseMenuController.IsPaused) return;

            Look();
            Move();
        }

        void Look()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 delta = Mouse.current.delta.ReadValue() * GameSettings.Sensitivity;

            // Yatay donusu govdeye, dikeyi kameraya uyguluyoruz; ikisini tek
            // transform'da toplarsak karakter yana yatiyor.
            transform.Rotate(Vector3.up, delta.x, Space.World);

            _pitch = Mathf.Clamp(_pitch - delta.y, -maxPitch, maxPitch);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>Yerdeyken ve hareket ederken belirli araliklarla adim calar.</summary>
        void AdimSesi(bool hareketVar, float speed)
        {
            if (!hareketVar || !_controller.isGrounded)
            {
                // Duran ya da havadaki oyuncuda sayaci sifirlamiyoruz ama
                // ilerletmiyoruz da; boylece durup tekrar yuruyunce ilk adim
                // hemen degil, yarim adim sonra geliyor - daha dogal.
                return;
            }

            _stepDistance += speed * Time.deltaTime;
            if (_stepDistance < StepStride) return;
            _stepDistance = 0f;

            var lib = SoundLibrary.Instance;
            if (lib == null) return;

            if (_world == null) _world = FindAnyObjectByType<VoxelWorld>();
            if (_world == null) return;

            // Ayagin bir tik altindaki blok: karakterin merkezi degil zemin
            // onemli, yoksa her zaman havayi orneklerdik.
            Vector3 p = transform.position + Vector3.down * (_controller.height * 0.5f + 0.2f);
            var zemin = _world.GetBlock(
                Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));

            if (zemin == BlockId.Air) return;

            AudioManager.Oynat(lib.AdimSesi(zemin), transform.position, 0.42f, 0.12f);
        }

        void Move()
        {
            var kb = Keyboard.current;

            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);

            Vector3 input = transform.right * x + transform.forward * z;
            if (input.sqrMagnitude > 1f) input.Normalize();   // capraz gidiste hizlanmayi engeller

            float speed = kb.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
            if (kb.leftShiftKey.isPressed && PlayerSkills.Instance != null)
                speed *= PlayerSkills.Instance.State.SprintMultiplier;

            if (_controller.isGrounded)
            {
                // Tam sifir yerine kucuk negatif: isGrounded'in kararli kalmasi icin
                // karakteri her karede zemine hafifce bastiriyoruz.
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;

                if (kb.spaceKey.wasPressedThisFrame)
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = input * speed + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            AdimSesi(input.sqrMagnitude > 0.01f, speed);
        }
    }
}
