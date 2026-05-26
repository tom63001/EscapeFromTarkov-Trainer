using EFT.Trainer.Configuration;
using EFT.Trainer.Extensions;
using JetBrains.Annotations;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.Features;

[UsedImplicitly]
internal class Flight : ToggleFeature
{
	public override string Name => "flight";
	public override string Description => "Noclip flight. Move freely through the air using camera-relative controls.";

	public override bool Enabled { get; set; } = false;
	public override KeyCode Key { get; set; } = KeyCode.F6;

	[ConfigurationProperty(Order = 20)]
	public KeyCode Forward { get; set; } = KeyCode.W;

	[ConfigurationProperty(Order = 21)]
	public KeyCode Backward { get; set; } = KeyCode.S;

	[ConfigurationProperty(Order = 22)]
	public KeyCode Left { get; set; } = KeyCode.A;

	[ConfigurationProperty(Order = 23)]
	public KeyCode Right { get; set; } = KeyCode.D;

	[ConfigurationProperty(Order = 24)]
	public KeyCode Ascend { get; set; } = KeyCode.Space;

	[ConfigurationProperty(Order = 25)]
	public KeyCode Descend { get; set; } = KeyCode.LeftControl;

	[ConfigurationProperty(Order = 26)]
	public KeyCode FastMode { get; set; } = KeyCode.LeftShift;

	[ConfigurationProperty(Order = 30)]
	public float MovementSpeed { get; set; } = 6f;

	[ConfigurationProperty(Order = 31)]
	public float FastMovementSpeed { get; set; } = 20f;

	[ConfigurationProperty(Order = 40)]
	public bool DisableCollisions { get; set; } = true;

	private Vector3? _flightPosition = null;
	private bool _wasActive = false;

	protected override void UpdateWhenEnabled()
	{
		var player = GameState.Current?.LocalPlayer;
		if (!player.IsValid())
			return;

		var camera = GameState.Current?.Camera;
		if (camera == null)
			return;

		_flightPosition ??= player.Transform.position;
		_wasActive = true;

		SetPlayerCollision(player, !DisableCollisions);

		var heading = GetMovementHeading(camera.transform);
		if (heading != Vector3.zero)
		{
			var speed = FastMode != KeyCode.None && Input.GetKey(FastMode)
				? Mathf.Max(MovementSpeed, FastMovementSpeed)
				: MovementSpeed;
			_flightPosition += heading.normalized * speed * Time.deltaTime;
		}

		player.Transform.position = _flightPosition.Value;
	}

	protected override void UpdateWhenDisabled()
	{
		if (!_wasActive)
			return;

		var player = GameState.Current?.LocalPlayer;
		if (player.IsValid() && FeatureFactory.GetFeature<NoCollision>() is not { Enabled: true })
			SetPlayerCollision(player, true);

		_flightPosition = null;
		_wasActive = false;
	}

	private Vector3 GetMovementHeading(Transform cameraTransform)
	{
		var heading = Vector3.zero;

		if (Input.GetKey(Forward))
			heading += cameraTransform.forward;

		if (Input.GetKey(Backward))
			heading -= cameraTransform.forward;

		if (Input.GetKey(Left))
			heading -= cameraTransform.right;

		if (Input.GetKey(Right))
			heading += cameraTransform.right;

		if (Input.GetKey(Ascend))
			heading += Vector3.up;

		if (Input.GetKey(Descend))
			heading -= Vector3.up;

		return heading;
	}

	private static void SetPlayerCollision(Player player, bool enabled)
	{
		foreach (var rigidbody in player.GetComponentsInChildren<Rigidbody>())
		{
			if (rigidbody.detectCollisions == enabled)
				continue;

			rigidbody.detectCollisions = enabled;
		}
	}
}
