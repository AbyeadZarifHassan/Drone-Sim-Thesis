import os
import xacro
from ament_index_python.packages import get_package_share_directory
from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument, IncludeLaunchDescription, SetEnvironmentVariable
from launch.launch_description_sources import PythonLaunchDescriptionSource
from launch.substitutions import LaunchConfiguration, PathJoinSubstitution
from launch_ros.actions import Node
from launch_ros.substitutions import FindPackageShare

def generate_launch_description():
    rover_description_dir = get_package_share_directory("rover_description")
    
    # Xacro file path
    xacro_file_path = os.path.join(rover_description_dir, "urdf", "rover_description.urdf.xacro")
    
    # Process xacro file
    robot_description_content = xacro.process_file(xacro_file_path).toxml()
    
    # Declare launch arguments
    use_sim_time_arg = DeclareLaunchArgument(
        name="use_sim_time",
        default_value="true",
        description="Use simulation clock if true"
    )
    
    world_arg = DeclareLaunchArgument(
        name="world",
        default_value="empty.sdf",
        description="Path to world file (SDF format)"
    )
    
    # Set Gazebo resource path - add parent directory so it can resolve package://rover_description
    gz_resource_path = SetEnvironmentVariable(
        name='GZ_SIM_RESOURCE_PATH',
        value=os.path.dirname(rover_description_dir)
    )
    
    # Start Gazebo with an empty world
    gazebo = IncludeLaunchDescription(
        PythonLaunchDescriptionSource([
            PathJoinSubstitution([
                FindPackageShare('ros_gz_sim'),
                'launch',
                'gz_sim.launch.py'
            ])
        ]),
        launch_arguments={
            'gz_args': LaunchConfiguration('world')
        }.items()
    )
    
    # Bridge between ROS 2 and Gazebo
    bridge = Node(
        package='ros_gz_bridge',
        executable='parameter_bridge',
        arguments=[
            '/clock@rosgraph_msgs/msg/Clock[gz.msgs.Clock',
        ],
        output='screen'
    )
    
    # Robot state publisher
    robot_state_publisher_node = Node(
        package="robot_state_publisher",
        executable="robot_state_publisher",
        parameters=[
            {"robot_description": robot_description_content},
            {"use_sim_time": LaunchConfiguration("use_sim_time")}
        ]
    )
    
    # Spawn robot in Gazebo
    spawn_entity_node = Node(
        package='ros_gz_sim',
        executable='create',
        arguments=[
            '-name', 'minirover',
            '-topic', 'robot_description',
            '-x', '0.0',
            '-y', '0.0',
            '-z', '0.0'
        ],
        output='screen'
    )
    
    return LaunchDescription([
        gz_resource_path,
        use_sim_time_arg,
        world_arg,
        gazebo,
        bridge,
        robot_state_publisher_node,
        spawn_entity_node
    ])