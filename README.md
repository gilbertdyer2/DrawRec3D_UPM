# DrawRec3D
*Note: This is the repository for DrawRec3D's Unity package. To use this, you will likely need additional tools for creating drawings that can be found in the main repo: https://github.com/gilbertdyer2/DrawRec3D (training scripts and more comprehensive documentation can be found here as well)*

\
A Unity3D tool and standalone model developed to support recognition of user-drawn 3D shapes. Useful for drawing/gesture-based AR/VR controls (e.g. controlling UIs with physical gestures, spawning 3D assets that matches a user's drawing), and general object detection. One of the focuses is ease of use - the included model is trained under a Siamese architecture for generalized recognition. This means the model is ready to use for new general 3D shapes and drawings. That being said, scripts to retrain the model with your own data are provided, along with an AR application to draw with controllers and save created drawings.

Below is a short demo video for another project called PicoTown, a mixed reality city-builder with a mechanic that utilizes DrawRec3D to match and transform user-created drawings to building assets.


https://github.com/user-attachments/assets/92afeac6-c6c5-44a3-be9e-5dcb6e73947f


## Setup


1. Install into a Unity project via UPM from the repository URL: `https://github.com/gilbertdyer2/DrawRec3D_UPM`

    *(Window->Package Manager->Click upper left '+' icon->Install package from git URL)*


2. Next, under `Assets/StreamingAssets`, create the directory path `DrawRec3D/RuntimeDrawings`. This is where you will insert drawings (represented by JSON files) to compare to during runtime.


3. Insert the DrawRec3D prefab into a scene. 


4. To query DrawRec3D for a match, obtain a `List<Vector3>` of points representing your drawing and call `DrawingRecognizer.GetMatch(points)` on the prefab's DrawingRecognizer component. This will find the closest match within `RuntimeDrawings/` and return a string of its respective filename.

\
\
Feel free to send any questions or suggestions to gilbertdyer13@gmail.com
