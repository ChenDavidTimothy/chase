using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Chase.Tests
{
    public sealed class ChasePracticePlaygroundPlayTests
    {
        [UnityTest] public IEnumerator AuthoredCrossingsSupportRealDiveAndSlideRoutes()
        {
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/PracticePlayground.unity");
            bool already=scene.IsValid() && scene.isLoaded;
            if(!already)
            {
                yield return SceneManager.LoadSceneAsync(
                    "PracticePlayground",LoadSceneMode.Additive);
                scene=SceneManager.GetSceneByName("PracticePlayground");
            }
            var old=Physics.simulationMode;
            try
            {
                var practice=scene.GetRootGameObjects()[0]
                    .GetComponentInChildren<ChasePracticePlayground>(true);
                Assert.NotNull(practice);
                Physics.simulationMode=SimulationMode.Script;
                const float dt=.02f;
                foreach(float x in new[]{-5f,5f})
                {
                    var motor=practice.Motor;
                    var beam=scene.GetRootGameObjects()[0]
                        .GetComponentsInChildren<BoxCollider>(true);
                    BoxCollider crossing=null;
                    foreach(var b in beam)
                        if(b.name=="Crossing beam / stand-blocking solid" &&
                           Mathf.Abs(b.transform.position.x-x)<.1f)
                        { crossing=b;break; }
                    Assert.NotNull(crossing,"Missing physical practice beam at x="+x);

                    motor.ResetMotor(new Vector3(x,.9f,-1.6f-2.7f));
                    Physics.SyncTransforms();
                    float clock=41f;
                    float progress=-100f;
                    int trueAirborne=0;
                    for(int i=0;i<130;i++)
                    {
                        motor.Simulate(new ChaseAction(Vector2.up,false,i==0,false,false),
                            dt,clock);
                        Physics.Simulate(dt);clock+=dt;
                        progress=Mathf.Max(progress,motor.Body.position.z+1.6f);
                        if(motor.DiveColliderActive &&
                           motor.Body.position.z>-2.2f &&
                           motor.Body.position.z<-.9f)
                        {
                            float bottom=motor.Body.position.y+motor.Capsule.center.y-
                                motor.Capsule.radius;
                            Assert.Greater(bottom,crossing.bounds.max.y+.002f);
                            trueAirborne++;
                        }
                    }
                    Assert.Greater(progress,1.5f,"Dive must clear crossing "+x);
                    Assert.Greater(trueAirborne,0,"Recorded actual aerial route at x="+x);

                    motor.ResetMotor(new Vector3(x,.9f,-1.6f-1.15f));
                    motor.Body.linearVelocity=Vector3.forward*6f;
                    Physics.SyncTransforms();
                    float maxSlide=-100f;
                    bool below=false,standing=false;
                    clock=65f;
                    for(int i=0;i<220;i++)
                    {
                        motor.Simulate(new ChaseAction(Vector2.up,false,false,i==0,false),
                            dt,clock);
                        Physics.Simulate(dt);clock+=dt;
                        float relative=motor.Body.position.z+1.6f;
                        maxSlide=Mathf.Max(maxSlide,relative);
                        if(relative>-.3f && relative<.5f)
                        {
                            Assert.IsTrue(motor.SlideColliderActive);
                            float highest=motor.Body.position.y+
                                motor.SlideBox.center.y+motor.SlideBox.size.y*.5f;
                            Assert.Less(highest,crossing.bounds.min.y+.005f);
                            below=true;
                        }
                        if(relative>1.5f && motor.State==ChaseMovementState.Standing)
                            standing=true;
                    }
                    Assert.IsTrue(below && standing && maxSlide>1.5f,
                        "Sliding beneath and recovering must be possible: x="+x);
                }
            }
            finally {Physics.simulationMode=old;Time.timeScale=1f;}
            if(!already && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest] public IEnumerator FreeRoamPreservesPositionAndPauseHasSafeExit()
        {
            var old=Time.timeScale;
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/PracticePlayground.unity");
            bool already=scene.IsValid()&&scene.isLoaded;
            if(!already)
            {
                yield return SceneManager.LoadSceneAsync(
                    "PracticePlayground",LoadSceneMode.Additive);
                scene=SceneManager.GetSceneByName("PracticePlayground");
            }
            try
            {
                var practice=scene.GetRootGameObjects()[0]
                    .GetComponentInChildren<ChasePracticePlayground>(true);
                Assert.NotNull(practice);
                Assert.NotNull(practice.Motor);
                Assert.IsEmpty(practice.GetComponentsInChildren<ChaseBot>());
                Assert.IsEmpty(practice.GetComponentsInChildren<ChaseTagContact>());
                Assert.IsTrue(practice.Motor.Capsule.enabled);
                Assert.AreEqual(1.8f,practice.Motor.Settings.standingHeight,.001f);
                Assert.AreEqual(7.25f,practice.Motor.Settings.leapVerticalSpeed,.001f);
                var view=UnityEngine.Camera.main.transform;
                Assert.That(view.eulerAngles.y,Is.EqualTo(0f).Within(.01f));
                Vector3 front=view.forward;front.y=0;front.Normalize();
                Assert.That(Vector3.Dot(front,Vector3.forward),
                    Is.GreaterThan(.9999f));

                // No station changes or rewind: the player remains wherever
                // they navigate until physically moving or leaving practice.
                var body=practice.Motor.Body;
                body.position=new Vector3(2.3f,.9f,1.1f);
                body.linearVelocity=new Vector3(3f,0f,0f);
                Physics.SyncTransforms();
                practice.TogglePause();
                Assert.IsTrue(practice.Paused);
                Assert.AreEqual(0f,Time.timeScale);
                Assert.Less(Vector3.Distance(body.position,
                    new Vector3(2.3f,.9f,1.1f)),.001f);
                practice.TogglePause();
                Assert.IsFalse(practice.Paused);
                Assert.AreEqual(1f,Time.timeScale);
                Assert.Less(Vector3.Distance(body.position,
                    new Vector3(2.3f,.9f,1.1f)),.001f);
                Assert.AreEqual(3f,body.linearVelocity.x,.01f);
                yield return null;
            }
            finally
            {
                Time.timeScale=old;
            }
            if(!already && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
