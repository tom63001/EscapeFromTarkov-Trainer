using System.Collections.Generic;
using EFT.CameraControl;
using EFT.Trainer.Configuration;
using EFT.Trainer.Extensions;
using EFT.Trainer.Properties;
using JetBrains.Annotations;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.Features;

[UsedImplicitly]
internal class FreeCamera : ToggleFeature
{
	private const string MouseXAxis = "Mouse X";
	private const string MouseYAxis = "Mouse Y";

	public override string Name => Strings.FeatureCameraName;
	public override string Description => Strings.FeatureCameraDescription;

	[ConfigurationProperty(Skip = true)] // we do not want to offer save/load support for this
	public override bool Enabled { get; set; } = false;

	[ConfigurationProperty(Order = 20)]
	public KeyCode Forward { get; set; } = KeyCode.UpArrow;

	[ConfigurationProperty(Order = 21)]
	public KeyCode Backward { get; set; } = KeyCode.DownArrow;

	[ConfigurationProperty(Order = 22)]
	public KeyCode Left { get; set; } = KeyCode.LeftArrow;

	[ConfigurationProperty(Order = 23)]
	public KeyCode Right { get; set; } = KeyCode.RightArrow;

	[ConfigurationProperty(Order = 24)]
	public KeyCode FastMode { get; set; } = KeyCode.RightShift;

	[ConfigurationProperty(Order = 25)]
	public KeyCode Teleport { get; set; } = KeyCode.T;

	[ConfigurationProperty(Order = 26)]
	public bool ShowPlayerBody { get; set; } = true;

	[ConfigurationProperty(Order = 30)]
	public float FreeLookSensitivity { get; set; } = 3f;

	[ConfigurationProperty(Order = 31)]
	public float MovementSpeed { get; set; } = 10f;

	[ConfigurationProperty(Order = 32)]
	public float FastMovementSpeed { get; set; } = 100f;

	private bool _usingThirdPersonBody = false;
	private Vector3? _freeCameraPosition = null;
	private Vector2? _freeCameraRotation = null;
	private readonly Dictionary<Renderer, bool> _playerRendererStates = [];

#pragma warning disable IDE0060
	[UsedImplicitly]
	private static bool SkipPlayerCameraControllerPrefix(PlayerCameraController __instance)
	{
		return FeatureFactory.GetFeature<FreeCamera>() is not { Enabled: true };
	}
#pragma warning restore IDE0060

	private void UpdatePlayerRenderState()
	{
		var player = GameState.Current?.LocalPlayer;
		if (!player.IsValid())
			return;

		if (!Enabled)
		{
			if (_usingThirdPersonBody)
			{
				RestoreFirstPersonBody(player);
				_usingThirdPersonBody = false;
			}

			RestorePlayerRenderers();
			return;
		}

		if (!ShowPlayerBody)
		{
			if (_usingThirdPersonBody)
			{
				RestoreFirstPersonBody(player);
				_usingThirdPersonBody = false;
			}

			SetPlayerBodyRendererVisibility(player, false);
			return;
		}

		RestorePlayerRenderers();
		ForceThirdPersonBody(player);
		_usingThirdPersonBody = true;
	}

	protected override void Update()
	{
		base.Update();

		UpdatePlayerRenderState();
	}

	protected override void UpdateWhenEnabled()
	{
		HarmonyPatchOnce(harmony =>
		{
			HarmonyPrefix(harmony, typeof(PlayerCameraController), nameof(PlayerCameraController.Update), nameof(SkipPlayerCameraControllerPrefix));
			HarmonyPrefix(harmony, typeof(PlayerCameraController), nameof(PlayerCameraController.LateUpdate), nameof(SkipPlayerCameraControllerPrefix));
			HarmonyPrefix(harmony, typeof(PlayerCameraController), nameof(PlayerCameraController.FixedUpdate), nameof(SkipPlayerCameraControllerPrefix));
			HarmonyPrefix(harmony, typeof(PlayerCameraController), nameof(PlayerCameraController.UpdatePointOfView), nameof(SkipPlayerCameraControllerPrefix));
		});
	}

	protected override void UpdateWhenDisabled()
	{
		_freeCameraPosition = null;
		_freeCameraRotation = null;
		RestorePlayerRenderers();
	}

	private void LateUpdate()
	{
		if (Enabled)
			UpdateFreeCamera();
	}

	private void UpdateFreeCamera()
	{
		var camera = GameState.Current?.Camera;
		if (camera == null)
			return;

		var fastMode = Input.GetKey(FastMode);
		var movementSpeed = fastMode ? FastMovementSpeed : MovementSpeed;

		var heading = Vector3.zero;
		var cameraTransform = camera.transform;
		_freeCameraPosition ??= cameraTransform.position;
		_freeCameraRotation ??= new Vector2(cameraTransform.localEulerAngles.y, cameraTransform.localEulerAngles.x);
		var cameraRotation = Quaternion.Euler(_freeCameraRotation.Value.y, _freeCameraRotation.Value.x, 0f);

		if (Input.GetKey(Left))
			heading = -(cameraRotation * Vector3.right);

		if (Input.GetKey(Right))
			heading = cameraRotation * Vector3.right;

		if (Input.GetKey(Forward))
			heading = cameraRotation * Vector3.forward;

		if (Input.GetKey(Backward))
			heading = -(cameraRotation * Vector3.forward);

		if (heading != Vector3.zero)
			_freeCameraPosition = _freeCameraPosition.Value + movementSpeed * Time.deltaTime * heading;

		var newRotationX = _freeCameraRotation.Value.x + Input.GetAxis(MouseXAxis) * FreeLookSensitivity;
		var newRotationY = _freeCameraRotation.Value.y - Input.GetAxis(MouseYAxis) * FreeLookSensitivity;
		_freeCameraRotation = new Vector2(newRotationX, newRotationY);
		cameraTransform.SetPositionAndRotation(_freeCameraPosition.Value, Quaternion.Euler(newRotationY, newRotationX, 0f));

		if (Input.GetKey(Teleport))
		{
			var player = GameState.Current?.LocalPlayer;
			if (!player.IsValid())
				return;

			var position = new Vector3(_freeCameraPosition.Value.x, _freeCameraPosition.Value.y - 2f, _freeCameraPosition.Value.z);
			var playerGameObject = player.gameObject;

			playerGameObject.transform.SetPositionAndRotation(position, Quaternion.Euler(newRotationY, newRotationX, 0f));
			RestoreFirstPersonBody(player);
			_usingThirdPersonBody = false;
			_freeCameraPosition = null;
			_freeCameraRotation = null;
			RestorePlayerRenderers();
			Enabled = false;
		}
	}

	private static void ForceThirdPersonBody(Player player)
	{
		player.SwitchRenderer(true);
		player.PlayerBody.UpdatePlayerRenders(EPointOfView.ThirdPerson, player.Side);

		foreach (var skin in player.PlayerBody.BodySkins.Values)
		{
			if (skin == null)
				continue;

			foreach (var renderer in skin.GetRenderers())
			{
				if (renderer == null)
					continue;

				renderer.allowOcclusionWhenDynamic = false;
				renderer.forceRenderingOff = false;
				renderer.enabled = true;
			}
		}
	}

	private static void RestoreFirstPersonBody(Player player)
	{
		player.SwitchRenderer(true);
		player.PlayerBody.UpdatePlayerRenders(EPointOfView.FirstPerson, player.Side);
	}

	private void SetPlayerBodyRendererVisibility(Player player, bool visible)
	{
		foreach (var skin in player.PlayerBody.BodySkins.Values)
		{
			if (skin == null)
				continue;

			foreach (var renderer in skin.GetRenderers())
			{
				if (renderer == null)
					continue;

				if (!_playerRendererStates.ContainsKey(renderer))
					_playerRendererStates[renderer] = renderer.enabled;

				renderer.enabled = visible;
			}
		}
	}

	private void RestorePlayerRenderers()
	{
		foreach (var rendererState in _playerRendererStates)
		{
			if (rendererState.Key == null)
				continue;

			rendererState.Key.enabled = rendererState.Value;
		}

		_playerRendererStates.Clear();
	}
}
