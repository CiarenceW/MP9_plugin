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
        private float hammer_accel = -5000;
        private float m_charging_handle_amount;
        private ModHelpEntry help_entry;
        public Sprite help_entry_sprite;
        private readonly float[] slide_push_hammer_curve = new float[] {
            0f,
            0f,
            0.35f,
            1f
        };
        private FieldInfo trigger_safety_field;
        private RotateMover trigger_safety = new RotateMover();
        public override ModHelpEntry GetGunHelpEntry()
        {
            return help_entry = new ModHelpEntry("MP9")
            {
                info_sprite = help_entry_sprite,
                title = "Heckler & Koch MR223",
                description = "Heckler & Koch Match Rifle .223 Remington\n"
                            + "Capacity: 10 + 1, 223 Remington\n"
                            + "\n"
                            + "Based on the AR-15 platform, the HK 416 improves on it thanks to its proprietary short-stroke gas piston, derived from the G36, itself derived from the AR-18. Thanks to this improvement, the H&K 416 outperformed the M4 in numerous tests conducted by the US Army's Delta Force.\n"
                            + "\n"
                            + "In 2007, H&K introduced the MR223, the civilian variant of the 416, to the European market. This variant would later come to the US under the name of MR556.\n"
                            + "\n"
                            + "In order to be compliant in states with stricter gun laws, civilians need to install special after-market parts, such as a pin that blocks the magazine from being removed until the receiver is opened, or a slide lock that locks open on every shot. Fortunately for them, loopholes that permit the rifle to function somewhat normally exist."
            };
        }
        public override LocaleTactics GetGunTactics()
        {
            return new LocaleTactics()
            {
                gun_internal_name = InternalName,
                title = "Heckler & Koch MR223\n",
                text = "A modded semi-auto rifle, made on a cheese-based diet\n" +
                       "A .223 Remington semi-auto rifle made for the European sporting market, this gun functions mechanically the same as the H&K 416, without the auto fire mode.\n" +
                       "\n" +
                       "This version of the gun is fitted with an auto-locking slide lock in an attempt to be California compliant.\n" +
                       "To safely holster the MR223, flip on the safety."
            };
        }
        public override void InitializeGun()
        {
            pooled_muzzle_flash = ((GunScript)ReceiverCoreScript.Instance().generic_prefabs.First(it => { return it is GunScript && ((GunScript)it).gun_model == GunModel.BerettaM9; })).pooled_muzzle_flash;
            //loaded_cartridge_prefab = ((GunScript)ReceiverCoreScript.Instance().generic_prefabs.First(it => { return it is GunScript && ((GunScript)it).gun_model == GunModel.Glock; })).loaded_cartridge_prefab;
        }
        public override void AwakeGun()
        {
            hammer.amount = 1;
            trigger_safety_field = typeof(GunScript).GetField("trigger_safety", BindingFlags.Instance | BindingFlags.NonPublic);
            trigger_safety = (RotateMover)trigger_safety_field.GetValue(this);
            trigger_safety.transform = transform.Find("trigger(linear)/trigger_safety");
            trigger_safety.rotations[0] = trigger_safety.transform.localRotation;
            trigger_safety.rotations[1] = transform.Find("trigger(linear)/trigger_safety_pressed").localRotation;
            trigger_safety.target_amount = 0f;
            trigger_safety.accel = -1f / (0.5f * (0.04f * 0.04f));
        }
        public override void UpdateGun()
        {
            ReceiverCoreScript.Instance().player.lah.main_camera.nearClipPlane = 0.02f; //I wanted to do that in the Awake method but apparently the main camera or lah is null or whatever so fuck me I guess I'm too tired for this

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

            if (slide.amount == 0 && _hammer_state == 3 && trigger.amount == 1)
            { // Simulate auto sear
                hammer.amount = Mathf.MoveTowards(hammer.amount, _hammer_cocked_val, Time.deltaTime * Time.timeScale * 50);
                if (hammer.amount == _hammer_cocked_val) _hammer_state = 2;
            }

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
                m_charging_handle_amount = slide.amount;
            }
            else
            {
                m_charging_handle_amount = Mathf.Min(m_charging_handle_amount, slide.amount);
            }
            if (trigger_safety.amount == 1f)
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
            trigger_safety.UpdateDisplay();

            ApplyTransform("charging_handle", m_charging_handle_amount, transform.Find("charging_handle"));
            ApplyTransform("locking_sear_spring_tige", slide_stop.amount, transform.Find("locking_sear_spring_tige"));

            hammer.UpdateDisplay();

            slide_stop.UpdateDisplay();

            UpdateAnimatedComponents();
        }
    }
}
