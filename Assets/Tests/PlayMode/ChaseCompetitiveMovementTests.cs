using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Chase.Tests
{
    // Real production PhysX, colliders and saved public arena fixtures.
    // Test approach envelopes, not merely one choreographed launch frame.
    public sealed class ChaseCompetitiveMovementTests
    {
        const float Dt=.02f;
        [UnityTest] public IEnumerator ResponsiveCutsPreserveSpeedCapAndPhysicalMomentum()
        {
            var former=Physics.simulationMode;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var athlete=new GameObject("competitive response probe");
            try
            {
                Physics.simulationMode=SimulationMode.Script;
                floor.transform.position=new Vector3(0,-.10f,0);
                floor.transform.localScale=new Vector3(80,.2f,80);
                athlete.transform.position=new Vector3(0,.9f,0);
                athlete.AddComponent<Rigidbody>();
                athlete.AddComponent<CapsuleCollider>();
                var visual=new GameObject("test visual");
                visual.transform.SetParent(athlete.transform,false);
                var motor=athlete.AddComponent<ChaseMotor>();
                motor.Initialize(new ChaseTuning(),visual.transform);
                Physics.SyncTransforms();
                float clock=25f;
                for(int i=0;i<9;i++)
                {
                    motor.Simulate(new ChaseAction(Vector2.right,false,false,false,false),Dt,clock);
                    Physics.Simulate(Dt);clock+=Dt;
                }
                Assert.Greater(motor.HorizontalVelocity.x,5.8f,
                    "Athlete must reach sprint quickly on deliberate WASD input");
                Assert.LessOrEqual(motor.HorizontalVelocity.magnitude,6.02f);
                for(int i=0;i<16;i++)
                {
                    motor.Simulate(new ChaseAction(Vector2.left,false,false,false,false),Dt,clock);
                    Physics.Simulate(Dt);clock+=Dt;
                }
                Assert.Less(motor.HorizontalVelocity.x,-5.7f,
                    "A reversal should complete in less than a third of a second");
                Assert.LessOrEqual(motor.HorizontalVelocity.magnitude,6.02f);
                // No invisible teleport or boosted dash authority.
                Assert.Greater(athlete.transform.position.x,-3f);
            }
            finally
            {
                Physics.simulationMode=former;
                UnityEngine.Object.DestroyImmediate(athlete);
                UnityEngine.Object.DestroyImmediate(floor);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator EarlyJumpDuringSlideGetupExecutesOnceAndNeverAutoRepeats()
        {
            var original=Physics.simulationMode;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var go=new GameObject("buffered competitive jump");
            try
            {
                Physics.simulationMode=SimulationMode.Script;
                floor.transform.position=new Vector3(0,-.10f,0);
                floor.transform.localScale=new Vector3(50,.2f,50);
                go.transform.position=new Vector3(0,.9f,0);
                go.AddComponent<Rigidbody>();go.AddComponent<CapsuleCollider>();
                var visual=new GameObject("visual").transform;
                visual.SetParent(go.transform,false);
                var m=go.AddComponent<ChaseMotor>();
                m.Initialize(new ChaseTuning(),visual);
                Physics.SyncTransforms();
                float clock=80f;
                m.Body.linearVelocity=Vector3.forward*6f;
                bool jumpQueued=false,launched=false;
                int jumpStarts=0;
                ChaseMovementState previous=m.State;
                for(int i=0;i<155;i++)
                {
                    bool slide=i==0;
                    // Press just before the normal slide recovery completes.
                    // A single early button press should be honored; keeping
                    // Space held must not create an automatic second jump.
                    bool jump=i>=25;
                    m.Simulate(new ChaseAction(Vector2.up,jump,false,slide,false),
                        Dt,clock);
                    Physics.Simulate(Dt);clock+=Dt;
                    if(i==25 && m.Recovering)jumpQueued=true;
                    if(previous!=ChaseMovementState.Jumping &&
                       m.State==ChaseMovementState.Jumping)
                    {
                        jumpStarts++;
                        launched=true;
                    }
                    previous=m.State;
                    Assert.IsTrue(float.IsFinite(m.Body.position.y));
                    Assert.Less(m.HorizontalVelocity.magnitude,6.05f);
                }
                Assert.IsTrue(jumpQueued,
                    "Press must occur before the mandatory slide recovery completes");
                Assert.IsTrue(launched,"A small early Space press must not be lost");
                Assert.AreEqual(1,jumpStarts,"Held Space must never become an auto-bunnyhop");
            }
            finally
            {
                Physics.simulationMode=original;
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(floor);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator RealPublicRailAndBenchAcceptAnApproachEnvelope()
        {
            bool existing=SceneManager.GetSceneByName("PracticePlayground").isLoaded;
            if(!existing)
                yield return SceneManager.LoadSceneAsync("PracticePlayground",LoadSceneMode.Additive);
            var scene=SceneManager.GetSceneByName("PracticePlayground");
            var old=Physics.simulationMode;
            try
            {
                Physics.simulationMode=SimulationMode.Script;
                var practice=scene.GetRootGameObjects()[0]
                    .GetComponentInChildren<ChasePracticePlayground>(true);
                Assert.NotNull(practice);
                var m=practice.Motor;
                foreach(float x in new[]{-5f,5f})
                {
                    int diveSuccess=0,slideSuccess=0;
                    float diveRecoverySeconds=0f,slideRecoverySeconds=0f;
                    foreach(float offset in new[]{-.23f,0f,.23f})
                    {
                        foreach(float distance in new[]{2.35f,2.70f,3.05f})
                        {
                            m.ResetMotor(new Vector3(x+offset,.9f,-1.6f-distance));
                            Physics.SyncTransforms();
                            float clock=30f;
                            bool clear=false,settled=false;
                            int firstRecovered=-1;
                            for(int i=0;i<125;i++)
                            {
                                m.Simulate(new ChaseAction(Vector2.up,false,i==0,false,false),Dt,clock);
                                Physics.Simulate(Dt);clock+=Dt;
                                clear|=m.Body.position.z>-1.6f+1.35f;
                                if(clear && m.State==ChaseMovementState.Standing)
                                { settled=true;if(firstRecovered<0)firstRecovered=i; }
                            }
                            if(clear&&settled)
                            { diveSuccess++; diveRecoverySeconds+=(firstRecovered+1)*Dt; }
                        }
                        foreach(float distance in new[]{.95f,1.15f,1.35f})
                        {
                            m.ResetMotor(new Vector3(x+offset,.9f,-1.6f-distance));
                            m.Body.linearVelocity=Vector3.forward*6f;
                            Physics.SyncTransforms();
                            float clock=40f;
                            bool clear=false,settled=false;
                            int firstRecovered=-1;
                            for(int i=0;i<125;i++)
                            {
                                m.Simulate(new ChaseAction(Vector2.up,false,false,i==0,false),Dt,clock);
                                Physics.Simulate(Dt);clock+=Dt;
                                clear|=m.Body.position.z>-1.6f+1.35f;
                                if(clear && m.State==ChaseMovementState.Standing)
                                { settled=true;if(firstRecovered<0)firstRecovered=i; }
                            }
                            if(clear&&settled)
                            { slideSuccess++; slideRecoverySeconds+=(firstRecovered+1)*Dt; }
                        }
                    }
                    Debug.Log("CHASE_COMPETITIVE_CROSSING_ENVELOPE x="+x+
                        " dive="+diveSuccess+"/9 slide="+slideSuccess+"/9"+
                        " avg_dive_recovery_s="+(diveRecoverySeconds/Mathf.Max(1,diveSuccess)).ToString("F2")+
                        " avg_slide_recovery_s="+(slideRecoverySeconds/Mathf.Max(1,slideSuccess)).ToString("F2"));
                    // Several viable offsets/timings on both routes, not a
                    // single exact keyboard frame on a single center line.
                    Assert.GreaterOrEqual(diveSuccess,5,
                        "Dive should work for several skillful approaches at "+x);
                    Assert.GreaterOrEqual(slideSuccess,5,
                        "Slide should work for several skillful approaches at "+x);
                }
            }
            finally {Physics.simulationMode=old;Time.timeScale=1f;}
            if(!existing && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
