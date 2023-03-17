using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Receiver2;
using Receiver2ModdingKit;
using UnityEngine;
using RewiredConsts;

namespace MP9_plugin
{
    public class MP9_plugin : ModGunScript
    {
        private float hammer_accel = -8000;
        private float m_charging_handle_amount;
        private float safety_held_time;
        private ModHelpEntry help_entry;
        private readonly float[] slide_push_hammer_curve = new float[] {
            0f,
            0f,
            0.35f,
            1f
        };
        private RotateMover stock = new RotateMover();
        private RotateMover trigger_safety = new RotateMover();
        public override ModHelpEntry GetGunHelpEntry()
        {
            return help_entry = new ModHelpEntry("MP9")
            {
                info_sprite = spawn_info_sprite,
                title = "Brügger & Thomet MP9",
                description = "Brügger & Thomet Maschinenpistole 9 mm\n"
                            + "Capacity: 30 + 1, 9x19mm NATO\n"
                            + "\n"
                            + "The MP9 is based on the TMP, which was originally designed as a PDW, unlike the P90 and in some way, the MP7, the TMP was more pistol than SMG. Its characteristics were amongst the likes of the MP5K, the Micro Uzi or the Ingram MAC, the latter displaying the same intentions as the TMP, however without managing to reach them. The TMP distinguished itself from the rest thanks to its weight, and ergonomics, and for being easily controlable for a gun of its category.\n"
                            + "\n"
                            + "In 2003, B&T, a swiss company, purchased the design of the TMP from Steyr, and added many improvements, such as a foldable stock, a Picatinny rail on the top of the receiver, and a trigger safety, thanks to these, the MP9 is much more polyvalent than the TMP, and as such, is a fitting weapon for law-enforcement.\n"
            };
        }
        public override LocaleTactics GetGunTactics()
        {
            return new LocaleTactics()
            {
                gun_internal_name = InternalName,
                title = "Brügger & Thomet MP9\n",
                text = "A modded full-auto capable SMG, made on a trip to Tulsey Town\n" +
                       "A 9mm SMG, manufactured as a further development from the TMP, for use by law-enforcement agencies.\n" +
                       "\n" +
                       "To safely holster the MP9, flip on the safety."
            };
        }
        public override void InitializeGun()
        {
            pooled_muzzle_flash = ((GunScript)ReceiverCoreScript.Instance().generic_prefabs.First(it => { return it is GunScript && ((GunScript)it).gun_model == GunModel.BerettaM9; })).pooled_muzzle_flash;
            //loaded_cartridge_prefab = ((GunScript)ReceiverCoreScript.Instance().generic_prefabs.First(it => { return it is GunScript && ((GunScript)it).gun_model == GunModel.Glock; })).loaded_cartridge_prefab;
            ReceiverCoreScript.Instance().GetMagazinePrefab("Ciarencew.MP9", MagazineClass.StandardCapacity).glint_renderer.material = ReceiverCoreScript.Instance().GetMagazinePrefab("wolfire.glock_17", MagazineClass.StandardCapacity).glint_renderer.material;
            ReceiverCoreScript.Instance().GetMagazinePrefab("Ciarencew.MP9", MagazineClass.LowCapacity).glint_renderer.material = ReceiverCoreScript.Instance().GetMagazinePrefab("wolfire.glock_17", MagazineClass.StandardCapacity).glint_renderer.material;
        }
        public override void AwakeGun()
        {
            hammer.amount = 1;
            stock.transform = transform.Find("stock");
            stock.rotations[0] = transform.Find("stock_unfolded").localRotation;
            stock.rotations[1] = transform.Find("stock_folded").localRotation;
            var trigger_safety_field = typeof(GunScript).GetField("trigger_safety", BindingFlags.Instance | BindingFlags.NonPublic);
            trigger_safety = (RotateMover)trigger_safety_field.GetValue(this);
            trigger_safety.transform = transform.Find("trigger(linear)/trigger_safety");
            trigger_safety.rotations[0] = trigger_safety.transform.localRotation;
            trigger_safety.rotations[1] = transform.Find("trigger(linear)/trigger_safety_pressed").localRotation;
            trigger_safety.target_amount = 0f;
            trigger_safety.accel = -1f / (0.5f * (0.04f * 0.04f));
            ReceiverEvents.StartListening(ReceiverEventTypeVoid.PlayerInitialized, ev =>
            {
                LocalAimHandler.player_instance.main_camera.nearClipPlane = 0.02f;
            });
            if (ReceiverCoreScript.Instance().player != null)
            {
                if (ReceiverCoreScript.Instance().player.lah.main_camera.nearClipPlane != 0.02f) ReceiverCoreScript.Instance().player.lah.main_camera.nearClipPlane = 0.02f; //I wanted to do that in the Awake method but apparently the main camera or lah is null or whatever so fuck me I guess I'm too tired for this
            }
            //player_script = (PlayerScript)typeof(LocalAimHandler).GetField("player_script", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ReceiverCoreScript.Instance().player.lah);
            //player_script.main_camera_prefab.GetComponent<Camera>().nearClipPlane = 0.02f;
        }
        public override void UpdateGun()
        {
            if (ReceiverCoreScript.Instance().player.lah.IsAiming()) two_handed = true; //makes the game two handed when aiming, no real reason, just thought it'd make more sense. Noticeable when holding a flashlight.
            else two_handed = false;



            hammer.asleep = true;
            hammer.accel = hammer_accel;

            if (slide.amount > 0 && _hammer_state != 3)
            { // Bolt cocks the hammer when moving back 
                hammer.amount = Mathf.Max(hammer.amount, InterpCurve(slide_push_hammer_curve, slide.amount));
            }

            firing_modes[0].sound_event_path = sound_safety_on;
            firing_modes[1].sound_event_path = sound_safety_off;
            firing_modes[2].sound_event_path = sound_safety_off;

            if (hammer.amount == 1) _hammer_state = 3;

            if (!IsSafetyOn())
            {
                if (player_input.GetButton(RewiredConsts.Action.Toggle_Safety_Auto_Mod))
                {
                    safety_held_time += Time.deltaTime;
                }
                else
                {
                    safety_held_time = 0;
                }

                if (safety_held_time >= 0.4f)
                {
                    SwitchFireMode();
                }
            }
            else
            {
                safety_held_time = 0;
            }
            if (IsSafetyOn())
            {
                trigger.amount = Mathf.Min(trigger.amount, 0.1f);

                trigger.UpdateDisplay();
            }
            if (_select_fire.amount == 0.5f)
            {
                trigger.amount = Mathf.Min(trigger.amount, 0.5f);

                trigger.UpdateDisplay();
            }

            ApplyTransform("connector_lever", trigger.amount, transform.Find("connector_lever"));
            ApplyTransform("sear", trigger.amount, transform.Find("sear"));
            if (_select_fire.amount == 1f && trigger.amount >= 0.1f) ApplyTransform("disconnectar_full", trigger.amount, transform.Find("disconnectar"));

            if (slide.amount == 0 && _hammer_state == 3 && trigger.amount == 1)
            { // Simulate auto sear
                hammer.amount = Mathf.MoveTowards(hammer.amount, _hammer_cocked_val, Time.deltaTime * Time.timeScale * 50);
                if (hammer.amount == _hammer_cocked_val) _hammer_state = 2;
            }

            if (_select_fire.amount < 1f && !_disconnector_needs_reset) ApplyTransform("disconnectar", hammer.amount, transform.Find("disconnectar"));

            if (hammer.amount == 0 && _hammer_state == 2)
            { // If hammer dropped and hammer was cocked then fire gun and decock hammer
                TryFireBullet(1, FireBullet);

                _hammer_state = 0;

                _disconnector_needs_reset = _select_fire.amount < 1f;
            }

            if (trigger.amount == 0)
            {
                _disconnector_needs_reset = false;
            }

            if (slide.amount > 0f && trigger.amount > 0f && _select_fire.amount < 1f)
            {
                _disconnector_needs_reset = true;
            }

            if (slide_stop.amount == 1)
            {
                slide_stop.asleep = true;
            }

            if (slide.amount == 0 && _hammer_state == 3 && _disconnector_needs_reset == false)
            {
                hammer.amount = Mathf.MoveTowards(hammer.amount, _hammer_cocked_val, Time.deltaTime * Time.timeScale * 50);
                if (hammer.amount == _hammer_cocked_val) _hammer_state = 2;
            }

            if (_hammer_state != 3 && ((trigger.amount >= 0.5f && !_disconnector_needs_reset && slide.amount == 0) || hammer.amount != _hammer_cocked_val))
            {
                hammer.asleep = false;
            }

            hammer.TimeStep(Time.deltaTime);

            if (player_input.GetButton(Action.Pull_Back_Slide) || player_input.GetButtonUp(Action.Pull_Back_Slide))
            {
                m_charging_handle_amount = Mathf.MoveTowards(m_charging_handle_amount, slide.amount, Time.deltaTime * 20f / Time.timeScale);
            }
            else
            {
                m_charging_handle_amount = Mathf.MoveTowards(m_charging_handle_amount, 0, Time.deltaTime * 50f);
            }
            /*if (trigger_safety.amount == 1f)
            {
                trigger.amount = Mathf.Max(trigger.target_amount, 1f);
            }
            if (trigger.amount != 0f)
            {
                trigger_safety.amount = Mathf.MoveTowards(trigger_safety.amount, 1f, Time.deltaTime * 10f / Time.timeScale);
            }
            else
            {
                trigger_safety.amount = Mathf.MoveTowards(trigger_safety.amount, 0f, Time.deltaTime * 10f);
            }
            trigger_safety.UpdateDisplay();*/

            if (player_input.GetButtonDown(14) && ReceiverCoreScript.Instance().player.lah.IsHoldingGun) //stock opening/closing logic
            {
                ToggleStock();
            }

            ApplyTransform("charging_handle", m_charging_handle_amount, transform.Find("charging_handle"));
            ApplyTransform("locking_sear_spring_tige", slide_stop.amount, transform.Find("locking_sear_spring_tige"));

            hammer.UpdateDisplay();

            slide_stop.UpdateDisplay();

            stock.UpdateDisplay();
            stock.TimeStep(Time.deltaTime);

            UpdateAnimatedComponents();
        }
        private void ToggleStock()
        {
            stock.asleep = false;
            if (stock.target_amount == 1f && slide.amount <= 0.03f)
            {
                stock.target_amount = 0f;
                stock.accel = -1f;
                stock.vel = -3f;
                AudioManager.PlayOneShotAttached(sound_safety_on, stock.transform.gameObject);
                rotation_transfer_y_min = 0.4f;
                rotation_transfer_y_max = 0.8f;
                rotation_transfer_x_min = -0.04f;
                rotation_transfer_x_max = 0.04f;
                recoil_transfer_x_min = 40f;
                recoil_transfer_x_max = 80f;
                recoil_transfer_y_min = -40f;
                recoil_transfer_y_max = 40f;
                sway_multiplier = 0.5f;
            }
            else if (stock.target_amount == 0f)
            {
                stock.target_amount = 1f;
                stock.accel = 1;
                stock.vel = 3;
                AudioManager.PlayOneShotAttached(sound_safety_off, stock.transform.gameObject);
                rotation_transfer_y_min = 2f;
                rotation_transfer_y_max = 4f;
                rotation_transfer_x_min = -0.2f;
                rotation_transfer_x_max = 0.2f;
                recoil_transfer_x_min = 100f;
                recoil_transfer_x_max = 200f;
                recoil_transfer_y_min = -100f;
                recoil_transfer_y_max = 100f;
                sway_multiplier = 2f;
            }

        }
    }
}
