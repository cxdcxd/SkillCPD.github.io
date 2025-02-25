﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;
using RRS.Tools.Network;
using RRS.Tools.Protobuf;
using UnityEngine;

public class Movo : MonoBehaviour
{
    #region Net2

    public ArticulationMove right_kinova;
    public ArticulationMove left_kinova;

    public GameObject nmpc_right_marker;
    public GameObject nmpc_left_marker;

    //PUBS
    Net2.Net2HandlerPublisher publisher_lidar_front;
    Net2.Net2HandlerPublisher publisher_lidar_rear;
    Net2.Net2HandlerPublisher publisher_camera_color;
    Net2.Net2HandlerPublisher publisher_camera_info;
    Net2.Net2HandlerPublisher publisher_camera_depth;
    Net2.Net2HandlerPublisher publisher_camera_segment;
    Net2.Net2HandlerPublisher publisher_camera_normal;
    Net2.Net2HandlerPublisher publisher_joint_state;
    Net2.Net2HandlerPublisher publisher_imu;
    Net2.Net2HandlerPublisher publisher_odometry;
    Net2.Net2HandlerPublisher publisher_tf;
    Net2.Net2HandlerPublisher publisher_groundtruth;
    Net2.Net2HandlerPublisher publisher_nmpc_in_right;
    Net2.Net2HandlerPublisher publisher_nmpc_in_left;
    public Net2.Net2HandlerPublisher publisher_cpd_command;

    //SUBS
    public Net2.Net2HandlerSubscriber subscriber_cpd_result;
    Net2.Net2HandlerSubscriber subscriber_cmd_vel;
    Net2.Net2HandlerSubscriber subscriber_planner_viz;
    Net2.Net2HandlerSubscriber subscriber_navigation_state;
    Net2.Net2HandlerSubscriber subscriber_rrs_command;
    Net2.Net2HandlerSubscriber subscriber_rrs_joint_command_left;
    Net2.Net2HandlerSubscriber subscriber_rrs_joint_command_right;
    Net2.Net2HandlerPublisher  publisher_cpd_results;

    #endregion

    void Start()
    {
        Statics.movo_ref = this;
    }

    public enum Links
    {
        left_arm_half_joint, 
        left_elbow_joint, 
        left_gripper_finger1_joint, 
        left_shoulder_lift_joint,
        left_shoulder_pan_joint, 
        left_wrist_3_joint,
        left_wrist_spherical_1_joint,
        left_wrist_spherical_2_joint,
        linear_joint, 
        pan_joint, 
        right_arm_half_joint, 
        right_elbow_joint, 
        right_gripper_finger1_joint,
        right_shoulder_lift_joint, 
        right_shoulder_pan_joint, 
        right_wrist_3_joint, 
        right_wrist_spherical_1_joint,
        right_wrist_spherical_2_joint, 
        tilt_joint,
        base_link,
        odom
    }

    [Range(1, 100)]
    public int fps_status = 50;

    float next_status_time = 0;

    [Range(1, 100)]
    public float fps_control_update = 50;

    float next_control_time = 0;

    [Range(1, 100)]
    public float fps_nav_path_update = 3;

    public GameObject target;
    public Camera sensor_kinect;
    public GameObject tag_test;
    public GameObject[] head_joints;
    public GameObject[] left_arm_joints;
    public GameObject[] right_arm_joints;
    public GameObject[] left_finger_joints;
    public GameObject[] right_finger_joints;
    public GameObject linear_joint;
    public CPDManager cpdmanager_ref;
    public HapticRender right_arm_force = new HapticRender();
    public HapticRender left_arm_force = new HapticRender();

    public Lidar lidar_front;
    public Lidar lidar_rear;
    public ColorCamera color_camera;
    ColorCamera depth_camera;
    ColorCamera segment_camera;
    ColorCamera normal_camera;
    public IMU imu;

    private RVector3[] current_path = null;
    private bool path_updated = false;
    private bool inited = false;
    private SVector3 current_position = new SVector3(0, 0, 0);
    private SVector4 current_orientation = new SVector4(0, 0, 0, 0);
    private GameObject _target;
    private GameObject _temp_target;
    private float timer_status = 0;
    private float timer_motor_update = 0;
    private float timer_nav_path = 0;
    private Vector3 speed = new Vector3(0, 0, 0);
    private Vector2 speed_head = new Vector2(0, 0);
    private Vector3 speed_right_hand = new Vector3(0, 0, 0);
    private Vector3 speed_left_hand = new Vector3(0, 0, 0);
    private float speed_right_gripper = 0;
    private float speed_left_gripper = 0;

    public GameObject ik_head_target;
    public GameObject ik_right_hand_target;
    public GameObject ik_left_hand_target;

    public ArticulationBody root_right_body;
    public ArticulationBody root_left_body;
    public ArticulationBody root_head_body;

    public Transform root_right_transform;
    public Transform root_left_transform;
    public Transform root_head_transform;

    public bool is_moving = false;

    public float[] d_joints;
    float[] c_joints;
    int joint_numbers = 19;

    public float stifness = 200;
    public float damping = 60;
    public float linear_friction = 0.05f;
    public float angular_friction = 0.05f;
    public float friction = 0.05f;
    public float target_velocity = 0.7f;

    void initJointsConfig()
    {

        foreach (var item in right_kinova.joints)
        {
            item.angularDamping = angular_friction;
            item.linearDamping = linear_friction;
            item.jointFriction = friction;

            ArticulationDrive drive = item.xDrive;
            drive.damping = damping;
            drive.stiffness = stifness;
            drive.targetVelocity = target_velocity;
            item.xDrive = drive;
        }

        foreach (var item in left_kinova.joints)
        {
            item.angularDamping = angular_friction;
            item.linearDamping = linear_friction;
            item.jointFriction = friction;

            ArticulationDrive drive = item.xDrive;
            drive.damping = damping;
            drive.stiffness = stifness;
            drive.targetVelocity = target_velocity;
            item.xDrive = drive;
        }

    }

    void init()
    {
        
        d_joints = new float[joint_numbers];
        c_joints = new float[joint_numbers];

        if (Statics.current_environment == Statics.Environments.Sim)
        {
            //publisher_cpd_command = Net2.Publisher("cpd_command");
            publisher_joint_state = Net2.Publisher("joint_state");
           // publisher_camera_color = Net2.Publisher("camera_color");
            //publisher_camera_info = Net2.Publisher("camera_info");
            publisher_nmpc_in_right = Net2.Publisher("nmpc_right_in");
            publisher_nmpc_in_left = Net2.Publisher("nmpc_left_in");
            //subscriber_rrs_command = Net2.Subscriber("rrs_ros-rrs_command");

            subscriber_rrs_joint_command_left = Net2.Subscriber("rrs_ros-joint_left");
            subscriber_rrs_joint_command_left.delegateNewData += Subscriber_rrs_joint_command_left_delegateNewData;

            subscriber_rrs_joint_command_right = Net2.Subscriber("rrs_ros-joint_right");
            subscriber_rrs_joint_command_right.delegateNewData += Subscriber_rrs_joint_command_right_delegateNewData;

            //subscriber_cpd_result = Net2.Subscriber("rrs_ros-cpd_result");
            //subscriber_cpd_result.delegateNewData += cpdmanager_ref.Subscriber_cpd_result_delegateNewData;

            //Sensors
            //color_camera.delegateCameraDataChanged += Color_camera_delegateCameraDataChanged;
        }
        else
        {
            right_arm_force = new HapticRender(Statics.right_container_distance);
            left_arm_force = new HapticRender(Statics.left_container_distance);
        }
    }

   

    public void Network_manager_movo_status_eventDataUpdated(MovoStatus status)
    {
        //print("Get status");

        for (int i = 0; i < 7; i++)
        {
            d_joints[i] = status.right_arm.joints[i].position * Mathf.Rad2Deg;
            //print(d_joints[i]);
        }

        for (int i = 8; i < 15; i++)
        {
            d_joints[i] = status.left_arm.joints[i - 8].position * Mathf.Rad2Deg;
            //print(d_joints[i]);

        }

        //print("===");
    }

    public void Network_manager_right_arm_eventDataUpdated(HapticRender render)
    {
        //Right Force
        //print("Get right");
        if (right_arm_force != null)
        right_arm_force.addMessurment(render);

       
    }

    public void Network_manager_left_arm_eventDataUpdated(HapticRender render)
    {
        //Left Force
        //print("Get left");
        if (left_arm_force != null)
        left_arm_force.addMessurment(render);


    }

    private void Normal_camera_delegateCameraDataChanged(byte[] buffer)
    {
        //publisher_camera_normal.Send(buffer);
    }

    #region MotorControl

   
    public bool enable_script_control = false;

    void updateMotors()
    {
      
        simpleviz();

        //initJointsConfig();

       



        //locomotion();

        //moveHead();

        //moveRightHand();
        //moveLeftHand();

        //rightGripper();
        //leftGripper();


    }

    #endregion

    #region SensorFeedbak
    private void Lidar_front_delegateLidarDataChanged(byte[] buffer)
    {
        //print("Send Lidar Front");
        publisher_lidar_front.Send(buffer);
    }

    private void Lidar_rear_delegateLidarDataChanged(byte[] buffer)
    {
        publisher_lidar_rear.Send(buffer);
    }

    private void Imu_delegateIMUDataChanged(byte[] buffer)
    {
        publisher_imu.Send(buffer);
    }

    private void Segment_camera_delegateCameraDataChanged(byte[] buffer)
    {
        //print("Segment");
        //publisher_camera_segment.Send(buffer);
    }

    private void Depth_camera_delegateCameraDataChanged(byte[] buffer)
    {
        //print("Depth");
        //publisher_camera_depth.Send(buffer);
    }

    private void Color_camera_delegateCameraDataChanged(byte[] buffer)
    {
        publisher_camera_color.Send(buffer);

        //Sending Camera Info

        RRSCameraInfo info_msg = new RRSCameraInfo();

        //width
        //800

        //height
        //600

        //[narrow_stereo]

        //Camera matrix
        //520.907761 0.000000 398.668872
        //0.000000 520.193056 298.836832
        //0.000000 0.000000 1.000000

        //Distortion
        //0.006218 -0.004135 0.000217 -0.000706 0.000000

        //Rectification
        //1.000000 0.000000 0.000000
        //0.000000 1.000000 0.000000
        //0.000000 0.000000 1.000000

        //Projection
        //522.240356 0.000000 397.936985 0.000000
        //0.000000 521.915222 299.013784 0.000000
        //0.000000 0.000000 1.000000 0.000000

        info_msg.width = 800;
        info_msg.height = 600;

        info_msg.P = new float[12];

        info_msg.P[0] = 522.240356f;
        info_msg.P[1] = 0.000000f;
        info_msg.P[2] = 397.936985f;
        info_msg.P[3] = 0.000000f;
        info_msg.P[4] = 0.000000f;
        info_msg.P[5] = 521.915222f;
        info_msg.P[6] = 299.013784f;
        info_msg.P[7] = 0.000000f;
        info_msg.P[8] = 0.000000f;
        info_msg.P[9] = 0.000000f;
        info_msg.P[10] = 1.000000f;
        info_msg.P[11] = 0.000000f;

        info_msg.R = new float[9];
        info_msg.R[0] = 1.000000f;
        info_msg.R[1] = 0.000000f;
        info_msg.R[2] = 0.000000f;
        info_msg.R[3] = 0.000000f;
        info_msg.R[4] = 1.000000f;
        info_msg.R[5] = 0.000000f;
        info_msg.R[6] = 0.000000f;
        info_msg.R[7] = 0.000000f;
        info_msg.R[8] = 1.000000f;

        info_msg.K = new float[9];
        info_msg.K[0] = 520.907761f;
        info_msg.K[1] = 0.000000f;
        info_msg.K[2] = 398.668872f;
        info_msg.K[3] = 0.000000f;
        info_msg.K[4] = 520.193056f;
        info_msg.K[5] = 298.836832f;
        info_msg.K[6] = 0.000000f;
        info_msg.K[7] = 0.000000f;
        info_msg.K[8] = 1.000000f;

        info_msg.D = new float[5];
        info_msg.D[0] = 0.006218f;
        info_msg.D[1] = -0.004135f;
        info_msg.D[2] = 0.000217f;
        info_msg.D[3] = -0.000706f;
        info_msg.D[4] = 0.000000f;

        info_msg.distortion_model = "plumb_bob";
      
        MemoryStream ms = new MemoryStream();
        ms = new MemoryStream();
        Serializer.Serialize<RRSCameraInfo>(ms, info_msg);

        byte[] data = ms.ToArray();

        publisher_camera_info.Send(data);
    }
    #endregion

    public GameObject[] movo_head;
    public GameObject[] movo_right_arm;
    public GameObject[] movo_left_arm;

    public float head_pan = 0;
    public float head_tilt = 0;

    public float right_arm_1 = 0;
    public float right_arm_2 = 0;
    public float right_arm_3 = 0;
    public float right_arm_4 = 0;
    public float right_arm_5 = 0;
    public float right_arm_6 = 0;
    public float right_arm_7 = 0;
    public float right_gripper = 0;

    public float left_arm_1 = 0;
    public float left_arm_2 = 0;
    public float left_arm_3 = 0;
    public float left_arm_4 = 0;
    public float left_arm_5 = 0;
    public float left_arm_6 = 0;
    public float left_arm_7 = 0;
    public float left_gripper = 0;

    void simpleviz()
    {
        if (Statics.current_environment == Statics.Environments.Sim)
        {
            if (enable_script_control)
            {
                right_arm_1 += d_joints[0] * Time.deltaTime;
                right_arm_2 += d_joints[1] * Time.deltaTime * -1;
                right_arm_3 += d_joints[2] * Time.deltaTime;
                right_arm_4 += d_joints[3] * Time.deltaTime;
                right_arm_5 += d_joints[4] * Time.deltaTime;
                right_arm_6 += d_joints[5] * Time.deltaTime;
                right_arm_7 += d_joints[6] * Time.deltaTime;

                left_arm_1 += d_joints[8] * Time.deltaTime;
                left_arm_2 += d_joints[9] * Time.deltaTime * -1;
                left_arm_3 += d_joints[10] * Time.deltaTime;
                left_arm_4 += d_joints[11] * Time.deltaTime;
                left_arm_5 += d_joints[12] * Time.deltaTime;
                left_arm_6 += d_joints[13] * Time.deltaTime;
                left_arm_7 += d_joints[14] * Time.deltaTime;
            }
        }
        else
        {
            right_arm_1 = d_joints[0];
            right_arm_2 = d_joints[1] * -1;
            right_arm_3 = d_joints[2];
            right_arm_4 = d_joints[3];
            right_arm_5 = d_joints[4];
            right_arm_6 = d_joints[5];
            right_arm_7 = d_joints[6];

            left_arm_1 = d_joints[8];
            left_arm_2 = d_joints[9] * -1;
            left_arm_3 = d_joints[10];
            left_arm_4 = d_joints[11];
            left_arm_5 = d_joints[12];
            left_arm_6 = d_joints[13];
            left_arm_7 = d_joints[14];
        }

        //head_pan = d_joints[17] * -1;
        //head_tilt = d_joints[18] * -1;

        //head_joints[0].transform.localRotation = Quaternion.Euler(0, head_pan, -180);
        //head_joints[1].transform.localRotation = Quaternion.Euler(90 + head_tilt,0 , 90);

        right_arm_joints[1].transform.localRotation = Quaternion.Euler(-180,right_arm_1,0);
        right_arm_joints[2].transform.localRotation = Quaternion.Euler(right_arm_2, 0, -90 );
        right_arm_joints[3].transform.localRotation = Quaternion.Euler(right_arm_3, 0, 90 );
        right_arm_joints[4].transform.localRotation = Quaternion.Euler(right_arm_4, 0, 90 );
        right_arm_joints[5].transform.localRotation = Quaternion.Euler(right_arm_5, 180, 90 );
        right_arm_joints[6].transform.localRotation = Quaternion.Euler(right_arm_6, 0, 90 );
        right_arm_joints[7].transform.localRotation = Quaternion.Euler(right_arm_7, 180, 90);

        left_arm_joints[1].transform.localRotation = Quaternion.Euler(-180, left_arm_1, 0);
        left_arm_joints[2].transform.localRotation = Quaternion.Euler(left_arm_2, 0, -90);
        left_arm_joints[3].transform.localRotation = Quaternion.Euler(left_arm_3, 0, 90);
        left_arm_joints[4].transform.localRotation = Quaternion.Euler(left_arm_4, 0, 90);
        left_arm_joints[5].transform.localRotation = Quaternion.Euler(left_arm_5, 180, 90);
        left_arm_joints[6].transform.localRotation = Quaternion.Euler(left_arm_6, 0, 90);
        left_arm_joints[7].transform.localRotation = Quaternion.Euler(left_arm_7, 180, 90);
    }


    void RotateToLinear(float primaryAxisRotation)
    {

    }

    private void Subscriber_rrs_joint_command_left_delegateNewData(long sequence, byte[] buffer, uint priority, Net2.Net2HandlerBase sender)
    {
        if (priority == 10) return;

        MemoryStream ms = new MemoryStream(buffer);
        RRSJointCommand cmd = Serializer.Deserialize<RRSJointCommand>(ms);

        for (int i = 8; i < 15; i++)
        {
            d_joints[i] = cmd.goal[i - 8];
        }

        //print("LEFT");
    }
    private void Subscriber_rrs_joint_command_right_delegateNewData(long sequence, byte[] buffer, uint priority, Net2.Net2HandlerBase sender)
    {
        if (priority == 10) return;

        MemoryStream ms = new MemoryStream(buffer);
        RRSJointCommand cmd = Serializer.Deserialize<RRSJointCommand>(ms);


        for (int i = 0; i < 7; i++)
        {
            d_joints[i] = cmd.goal[i];
        }

        //print("RIGHT");
    }


   

    private void Subscriber_navigation_state_delegateNewData(long sequence, byte[] buffer, uint priority, Net2.Net2HandlerBase sender)
    {
        //Navigation Status
        //print("Get Navigation Status");
    }

    private void Subscriber_planner_viz_delegateNewData(long sequence, byte[] buffer, uint priority, Net2.Net2HandlerBase sender)
    {
        if (priority == 10) return;
        MemoryStream ms = new MemoryStream(buffer);

        try
        {
            RRSRobot cmd = Serializer.Deserialize<RRSRobot>(ms);
            current_path = (RVector3[])cmd.path.Clone();
            path_updated = true;
            //print("Path serialized done");
        }
        catch (Exception e)
        {
            //print("error : " + e.Message);
            //string msg = e.Message;
        }
    }

    void sendNMPCMarkers()
    {
        //Right
        RVector7 right_marker = new RVector7();

        var c_pose = Helper.Unity2Ros(nmpc_right_marker.transform.localPosition);

        right_marker.x = c_pose.x;
        right_marker.y = c_pose.y;
        right_marker.z = c_pose.z;

        var c_rot = Helper.Unity2Ros(nmpc_right_marker.transform.localRotation);

        right_marker.qx = c_rot.x;
        right_marker.qy = c_rot.y;
        right_marker.qz = c_rot.z;
        right_marker.qw = c_rot.w;

        MemoryStream ms = new MemoryStream();
        Serializer.Serialize<RVector7>(ms, right_marker);
        byte[] data_right = ms.ToArray();

        //Left
        RVector7 left_marker = new RVector7();

        c_pose = Helper.Unity2Ros(nmpc_left_marker.transform.localPosition);

        left_marker.x = c_pose.x;
        left_marker.y = c_pose.y;
        left_marker.z = c_pose.z;

        c_rot = Helper.Unity2Ros(nmpc_left_marker.transform.localRotation);

        left_marker.qx = c_rot.x;
        left_marker.qy = c_rot.y;
        left_marker.qz = c_rot.z;
        left_marker.qw = c_rot.w;

        ms = new MemoryStream();
        Serializer.Serialize<RVector7>(ms, left_marker);
        byte[] data_left = ms.ToArray();

        if (Statics.current_environment == Statics.Environments.Sim)
        {
            publisher_nmpc_in_right.Send(data_right);
            publisher_nmpc_in_left.Send(data_left);
        }
        else
        {
            HapticCommand right_command = new HapticCommand();
            right_command.position = new RVector3();
            right_command.rotation_q = new SVector4();

            right_command.position.x = right_marker.x;
            right_command.position.y = right_marker.y;
            right_command.position.z = right_marker.z;

            right_command.rotation_q.x = right_marker.qx;
            right_command.rotation_q.y = right_marker.qy;
            right_command.rotation_q.z = right_marker.qz;
            right_command.rotation_q.w = right_marker.qw;

            //print("Send real message to right arm");
            Statics.network_manager_right_arm.sendMessage(right_command);

            HapticCommand left_command = new HapticCommand();
            left_command.position = new RVector3();
            left_command.rotation_q = new SVector4();

            left_command.position.x = left_marker.x;
            left_command.position.y = left_marker.y;
            left_command.position.z = left_marker.z;


            left_command.rotation_q.x = left_marker.qx;
            left_command.rotation_q.y = left_marker.qy;
            left_command.rotation_q.z = left_marker.qz;
            left_command.rotation_q.w = left_marker.qw;

            //print("Send real message to left arm");
            Statics.network_manager_left_arm.sendMessage(left_command);
        }

        //print("published");
    }

    private void sendState()
    {
        //sendGroundtruth();
        if (Statics.current_environment == Statics.Environments.Sim)
        sendJointState();
        //sendJointTF();
        sendNMPCMarkers();
    }

    private void Subscriber_cmd_vel_delegateNewData(long sequence, byte[] buffer, uint priority, Net2.Net2HandlerBase sender)
    {
        MemoryStream ms = new MemoryStream(buffer);
        RVector3 cmd = Serializer.Deserialize<RVector3>(ms);

        speed.x = cmd.x * 15;
        speed.y = cmd.y * 15; 
        speed.z = cmd.z * 10;

        //print(speed.x + " " + speed.y + " " + speed.z);
    }

    public void manualCmdVel(Vector3 speed)
    {
        this.speed.x = speed.x;
        this.speed.y = speed.y;
        this.speed.z = speed.z; 
    }

    public void manualHead(Vector2 speed)
    {
        this.speed_head.x = speed.x;
        this.speed_head.y = speed.y;
    }

    RRSTransform getRRSTransform(Transform t,Links name)
    {
        RRSTransform rtransform = new RRSTransform();

        Quaternion ros_q = Helper.Unity2Ros(t.localRotation);
        Vector3 ros_p = Helper.Unity2Ros(t.localPosition);

        rtransform.position = new RRS.Tools.Protobuf.SVector3(ros_p.x, ros_p.y, ros_p.z);
        rtransform.orientation = new RRS.Tools.Protobuf.SVector4(ros_q.x, ros_q.y, ros_q.z, ros_q.w);
       
        return rtransform;
    }

    void sendJointTF()
    {
        RRSTF tf_msg = new RRSTF();
        tf_msg.names = new string[1];
        tf_msg.parents = new string[1];
        tf_msg.transforms = new RRSTransform[1];

        tf_msg.names[0] = Links.base_link.ToString();
        tf_msg.parents[0] = Links.odom.ToString();
        tf_msg.transforms[0] = getRRSTransform(transform, Links.base_link);

        MemoryStream ms = new MemoryStream();
        Serializer.Serialize<RRSTF>(ms, tf_msg);
        byte[] data = ms.ToArray();

        ////print("publish tf");
        //publisher_tf.Send(data);
    }

    void sendGroundtruth()
    {
        RRSTransform t = new RRSTransform();

        t.position = new RRS.Tools.Protobuf.SVector3();
        t.orientation = new RRS.Tools.Protobuf.SVector4();
        Vector3 convertp = new Vector3();
      
        convertp = Helper.Unity2Ros(transform.position);

        t.position.x = convertp.x;
        t.position.y = convertp.y;
        t.position.z = convertp.z;

        var convertr = Helper.Unity2Ros(transform.rotation);
        t.orientation.x = convertr.x;
        t.orientation.y = convertr.y;
        t.orientation.z = convertr.z;
        t.orientation.w = convertr.w;

        MemoryStream ms = new MemoryStream();
        Serializer.Serialize<RRSTransform>(ms, t);
        byte[] data = ms.ToArray();

        //publisher_groundtruth.Send(data);
    }

    (float, float, float) CurrentPrimaryAxisRotationLinear()
    {
        float currentRotation = 0, currentEffort = 0, currentVel = 0;
        return (currentRotation, currentVel, currentEffort);
    }

   

    void sendJointState()
    {
        RRSJointState joint_state_msg = new RRSJointState();

        joint_state_msg.name = new string[joint_numbers];
        joint_state_msg.position = new float[joint_numbers];
        joint_state_msg.velocity = new float[joint_numbers];
        joint_state_msg.effort = new float[joint_numbers];

        joint_state_msg.name[0] = Links.right_shoulder_pan_joint.ToString();
        joint_state_msg.name[1] = Links.right_shoulder_lift_joint.ToString();
        joint_state_msg.name[2] = Links.right_arm_half_joint.ToString();
        joint_state_msg.name[3] = Links.right_elbow_joint.ToString();
        joint_state_msg.name[4] = Links.right_wrist_spherical_1_joint.ToString();
        joint_state_msg.name[5] = Links.right_wrist_spherical_2_joint.ToString();
        joint_state_msg.name[6] = Links.right_wrist_3_joint.ToString();
        joint_state_msg.name[7] = Links.right_gripper_finger1_joint.ToString();

        joint_state_msg.name[8] = Links.left_shoulder_pan_joint.ToString();
        joint_state_msg.name[9] = Links.left_shoulder_lift_joint.ToString();
        joint_state_msg.name[10] = Links.left_arm_half_joint.ToString();
        joint_state_msg.name[11] = Links.left_elbow_joint.ToString();
        joint_state_msg.name[12] = Links.left_wrist_spherical_1_joint.ToString();
        joint_state_msg.name[13] = Links.left_wrist_spherical_2_joint.ToString();
        joint_state_msg.name[14] = Links.left_wrist_3_joint.ToString();
        joint_state_msg.name[15] = Links.left_gripper_finger1_joint.ToString();

        joint_state_msg.name[16] = Links.linear_joint.ToString();
        joint_state_msg.name[17] = Links.pan_joint.ToString();
        joint_state_msg.name[18] = Links.tilt_joint.ToString();

        //print(right_arm_1 * Mathf.Deg2Rad);
        //print(right_arm_2 * Mathf.Deg2Rad * -1);

        (joint_state_msg.position[0], joint_state_msg.velocity[0], joint_state_msg.effort[0]) = (right_arm_1 * Mathf.Deg2Rad , 0, 0 );
        (joint_state_msg.position[1], joint_state_msg.velocity[1], joint_state_msg.effort[1]) = (right_arm_2 * Mathf.Deg2Rad * -1, 0, 0);
        (joint_state_msg.position[2], joint_state_msg.velocity[2], joint_state_msg.effort[2]) = (right_arm_3 * Mathf.Deg2Rad, 0, 0);
        (joint_state_msg.position[3], joint_state_msg.velocity[3], joint_state_msg.effort[3]) = (right_arm_4 * Mathf.Deg2Rad, 0, 0);
        (joint_state_msg.position[4], joint_state_msg.velocity[4], joint_state_msg.effort[4]) = (right_arm_5 * Mathf.Deg2Rad, 0, 0);
        (joint_state_msg.position[5], joint_state_msg.velocity[5], joint_state_msg.effort[5]) = (right_arm_6 * Mathf.Deg2Rad, 0, 0);
        (joint_state_msg.position[6], joint_state_msg.velocity[6], joint_state_msg.effort[6]) = (right_arm_7 * Mathf.Deg2Rad, 0, 0);

        (joint_state_msg.position[7], joint_state_msg.velocity[7], joint_state_msg.effort[7]) = (0, 0, 0);

        (joint_state_msg.position[8], joint_state_msg.velocity[8], joint_state_msg.effort[8]) = (left_arm_1 * Mathf.Deg2Rad, 0, 0);
        (joint_state_msg.position[9], joint_state_msg.velocity[9], joint_state_msg.effort[9]) = (left_arm_2 * Mathf.Deg2Rad * -1, 0, 0);
        (joint_state_msg.position[10], joint_state_msg.velocity[10], joint_state_msg.effort[10]) = (left_arm_3 * Mathf.Deg2Rad, 0, 0);
        (joint_state_msg.position[11], joint_state_msg.velocity[11], joint_state_msg.effort[11]) = (left_arm_4 * Mathf.Deg2Rad, 0, 0);
        (joint_state_msg.position[12], joint_state_msg.velocity[12], joint_state_msg.effort[12]) = (left_arm_5 * Mathf.Deg2Rad, 0, 0);
        (joint_state_msg.position[13], joint_state_msg.velocity[13], joint_state_msg.effort[13]) = (left_arm_6 * Mathf.Deg2Rad, 0, 0);
        (joint_state_msg.position[14], joint_state_msg.velocity[14], joint_state_msg.effort[14]) = (left_arm_7 * Mathf.Deg2Rad, 0, 0);

        (joint_state_msg.position[15], joint_state_msg.velocity[15], joint_state_msg.effort[15]) = (0, 0, 0);

        (joint_state_msg.position[16], joint_state_msg.velocity[16], joint_state_msg.effort[16]) = (0, 0, 0);
        (joint_state_msg.position[17], joint_state_msg.velocity[17], joint_state_msg.effort[17]) = (0, 0, 0);
        (joint_state_msg.position[18], joint_state_msg.velocity[18], joint_state_msg.effort[18]) = (0, 0, 0);

        //if (is_moving == false)
        //{
        //    for (int i = 0; i < joint_state_msg.position.Length; i++)
        //    {
        //        c_joints[i] = joint_state_msg.position[i];
        //    }
        //}
        //else
        //{
        //    for (int i = 0; i < joint_state_msg.position.Length; i++)
        //    {
        //        joint_state_msg.position[i] = c_joints[i];
        //    }
        //}

        //print(joint_state_msg.position[1]);

        MemoryStream ms = new MemoryStream();
        Serializer.Serialize<RRSJointState>(ms, joint_state_msg);
        byte[] data = ms.ToArray();

        publisher_joint_state.Send(data);
    }

   
    void Update()
    {
        timer_status += Time.deltaTime;
        timer_motor_update += Time.deltaTime;

        current_position.x = transform.position.x;
        current_position.y = transform.position.y;
        current_position.z = transform.position.z;

        current_orientation.x = transform.rotation.x;
        current_orientation.y = transform.rotation.y;
        current_orientation.z = transform.rotation.z;
        current_orientation.w = transform.rotation.w;

        if ( inited && Time.time >= next_status_time )
        {
            next_status_time = Time.time + (1 / fps_status);
            sendState();
            updateMotors();
        }

       
        if (Manager.inited && !inited)
        {
            init();
            inited = true;
        }
    }

    private float originalWidth = 1000;
    private float originalHeight = 800;

    [DebugGUIGraph(min: -1, max: 1, r: 0, g: 1, b: 0, group:0, autoScale: true)]
    float lm = 0;
  
    [DebugGUIGraph(min: -1, max: 1, r: 0, g: 1, b: 0, group: 1, autoScale: true)]
    float rm = 0;
   
    private void OnGUI()
    {
        GUI.skin.label.fontSize = 22;
        GUI.contentColor = Color.black;

        Vector3 scale = new Vector3();

        scale.x = Screen.width / originalWidth;
        scale.y = Screen.height / originalHeight;
        scale.z = 1.0f;

        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);


        if (left_arm_force != null && right_arm_force != null)
        {
             lm = left_arm_force.forceMagnitude() * 1000;
             rm = right_arm_force.forceMagnitude() * 1000;

            //print("Left Check :" + left_arm_force.check);
            //print("Right Check :" + right_arm_force.check);

            //GUI.Label(new Rect(10, 50, 2000, 100), "Right Hand Weight: " + rm.ToString("N2"));
            //GUI.Label(new Rect(10, 75, 2000, 100), "Left Hand Weight: " + lm.ToString("N2"));
        }
    }
}
