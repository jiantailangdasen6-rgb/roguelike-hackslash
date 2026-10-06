using System.Collections;
using Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Smoke tests for the M0 sandbox scene, driven by virtual input devices.</summary>
    public class PlayerSandboxTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private Mouse _mouse;
        private Rigidbody2D _body;
        private PlayerAim _aim;

        // Loaded inside each test rather than [UnitySetUp] so it runs after InputTestFixture.Setup.
        private IEnumerator LoadSandbox()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("Dungeon");
            yield return null;

            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Dungeon scene has no player");
            _body = player.GetComponent<Rigidbody2D>();
            _aim = player.GetComponent<PlayerAim>();
        }

        [UnityTest]
        public IEnumerator HoldingD_MovesPlayerRight()
        {
            yield return LoadSandbox();
            float startX = _body.position.x;

            Press(_keyboard.dKey);
            yield return new WaitForSeconds(0.5f);
            Release(_keyboard.dKey);

            Assert.That(_body.position.x - startX, Is.GreaterThan(2f));
        }

        [UnityTest]
        public IEnumerator Walls_StopThePlayer()
        {
            yield return LoadSandbox();

            Press(_keyboard.dKey);
            yield return new WaitForSeconds(3f);
            Release(_keyboard.dKey);

            // Right wall's inner face is at x = 11; player radius is 0.4.
            Assert.That(_body.position.x, Is.InRange(10f, 10.7f));
        }

        [UnityTest]
        public IEnumerator Dodge_DashesInFacingDirection()
        {
            yield return LoadSandbox();
            float startY = _body.position.y;

            Press(_keyboard.spaceKey); // no movement input -> dash toward default facing (down)
            Release(_keyboard.spaceKey);
            yield return new WaitForSeconds(0.4f);

            Assert.That(startY - _body.position.y, Is.GreaterThan(2f));
        }

        [UnityTest]
        public IEnumerator Aim_FollowsMouseCursor()
        {
            yield return LoadSandbox();
            for (int i = 0; i < 10; i++) yield return null; // let the follow camera settle

            var camera = Camera.main;
            Vector3 rightOfPlayer = (Vector3)_body.position + Vector3.right * 3f;
            Set(_mouse.position, (Vector2)camera.WorldToScreenPoint(rightOfPlayer));
            yield return null;

            Assert.That(_aim.AimDirection.x, Is.GreaterThan(0.9f));
        }
    }
}
