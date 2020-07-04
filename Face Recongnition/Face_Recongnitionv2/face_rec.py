import os
import cv2
import face_recognition
import numpy as np
from tqdm import tqdm
from collections import defaultdict
from imutils.video import VideoStream
from eye_status import * 
import time

class Initializer():
    def __init__(self):
        self.face_cascPath = 'haarcascade/haarcascade_frontalface_alt.xml'
        self.open_eye_cascPath = 'haarcascade/haarcascade_eye_tree_eyeglasses.xml'
        self.left_eye_cascPath = 'haarcascade/haarcascade_lefteye_2splits.xml'
        self.right_eye_cascPath ='haarcascade/haarcascade_righteye_2splits.xml'
        self.dataset = 'faces'

        self.face_detector = cv2.CascadeClassifier(self.face_cascPath)
        self.open_eyes_detector = cv2.CascadeClassifier(self.open_eye_cascPath)
        self.left_eye_detector = cv2.CascadeClassifier(self.left_eye_cascPath)
        self.right_eye_detector = cv2.CascadeClassifier(self.right_eye_cascPath)

        print("[LOG] Opening webcam ...")
        self.video_capture = VideoStream(src=0).start()

        print("[LOG] Loading Eye Blink Model Weights")
        self.model = load_model()
        
        print("[LOG] Collecting images ")
        self.images = []
        for direc, _, files in tqdm(os.walk(self.dataset)):
            for file in files:
                if file.endswith("jpg") or file.endswith("JPG"):
                    self.images.append(os.path.join(direc,file))
        
        print("[LOG] Initialization Complete") 

    def getInitialized_data(self):
        return (self.model,
                self.face_detector, 
                self.open_eyes_detector, 
                self.left_eye_detector,
                self.right_eye_detector, 
                self.video_capture, 
                self.images) 

    def process_and_encode(self,images):
        known_encodings = []
        known_names = []
        print("[LOG] Encoding faces ...")

        for image_path in tqdm(images):
            image = cv2.imread(image_path)
            image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
            boxes = face_recognition.face_locations(image, model='hog')
            encoding = face_recognition.face_encodings(image, boxes)
            name = image_path.split(os.path.sep)[-1].split(".")[-2]
            if len(encoding) > 0 : 
                known_encodings.append(encoding[0])
                known_names.append(name)

        return {"encodings": known_encodings, "names": known_names}

class Eye_Detector():
    def __init__(self):
        pass

    def Initilize_EyeHaarcascade(self,frame,gray,x,y,w,h):
        left_face = frame[y:y+h, x+int(w/2):x+w]
        left_face_gray = gray[y:y+h, x+int(w/2):x+w]

        right_face = frame[y:y+h, x:x+int(w/2)]
        right_face_gray = gray[y:y+h, x:x+int(w/2)]

        left_eye = left_eye_detector.detectMultiScale(
                    left_face_gray,
                    scaleFactor=1.1,
                    minNeighbors=5,
                    minSize=(30, 30),
                    flags = cv2.CASCADE_SCALE_IMAGE
        )

        right_eye = right_eye_detector.detectMultiScale(
            right_face_gray,
            scaleFactor=1.1,
            minNeighbors=5,
            minSize=(30, 30),
            flags = cv2.CASCADE_SCALE_IMAGE
        )

        return left_eye,right_eye,left_face,right_face

    def isBlinking(self, history, maxFrames):
        for i in range(maxFrames):
            pattern = '1' + '0'*(i+1) + '1'
            if pattern in history:
                return True
        return False

    def Both_Eyes_Open(self,gray_face):
        open_eyes_glasses = open_eyes_detector.detectMultiScale(
            gray_face,
            scaleFactor=1.1,
            minNeighbors=5,
            minSize=(30, 30),
            flags = cv2.CASCADE_SCALE_IMAGE
        )
        return open_eyes_glasses
        
    def Right_Eye_Open_Detect(self,right_eye,right_face):
        eye_status = '1'
        for (ex,ey,ew,eh) in right_eye:
            color = (0,255,0)
            pred = predict(right_face[ey:ey+eh,ex:ex+ew],model)
            if pred == 'closed':
                eye_status='0'
                color = (0,0,255)
            cv2.rectangle(right_face,(ex,ey),(ex+ew,ey+eh),color,2)
        return eye_status

    def Left_Eye_Open_Detect(self,left_eye,left_face):
        eye_status = '1'
        for (ex,ey,ew,eh) in left_eye:
            color = (0,255,0)
            pred = predict(left_face[ey:ey+eh,ex:ex+ew],model)
            if pred == 'closed':
                eye_status='0'
                color = (0,0,255)
            cv2.rectangle(left_face,(ex,ey),(ex+ew,ey+eh),color,2)
        return eye_status


class Detector():
    def __init__(self):
        self.eye_status = '1'

    def Compare_face(self,data,encoding):
        matches = face_recognition.compare_faces(data["encodings"], encoding)
        name = "Unknown"
        if True in matches:
            matchedIdxs = [i for (i, b) in enumerate(matches) if b]
            counts = {}
            for i in matchedIdxs:
                name = data["names"][i]
                counts[name] = counts.get(name, 0) + 1

            name = max(counts, key=counts.get)
        return name

    def frame_resize(self,video_capture):
        frame = video_capture.read()
        frame = cv2.resize(frame, (0, 0), fx=0.6, fy=0.6)
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        return frame,gray,rgb

    def detect_faces(self,face_detector,gray):
        faces = face_detector.detectMultiScale(
            gray,
            scaleFactor=1.2,
            minNeighbors=5,
            minSize=(50, 50),
            flags=cv2.CASCADE_SCALE_IMAGE
        )
        return faces

    def display_name(self,frame,text,x,y,w,h):
        cv2.rectangle(frame, (x, y), (x+w, y+h), (0, 255, 0), 2)
        y = y - 15 if y - 15 > 15 else y + 15
        cv2.putText(frame, text, (x, y), cv2.FONT_HERSHEY_SIMPLEX,0.75, (0, 255, 0), 2)
        print("[LOG] ",text)

    def detect_and_display(self,model, video_capture, face_detector, open_eyes_detector, left_eye_detector, right_eye_detector, data, eyes_detected, timer_check):
        frame,gray,rgb = self.frame_resize(video_capture)
        faces = self.detect_faces(face_detector,gray)
        print("[LOG] Faces Length: ", len(faces))
        eye_detector = Eye_Detector()

        for (x,y,w,h) in faces:
            encoding = face_recognition.face_encodings(rgb, [(y, x+w, y+h, x)])[0]
            name = self.Compare_face(data,encoding)
            face = frame[y:y+h,x:x+w]
            gray_face = gray[y:y+h,x:x+w]
            eyes = []

            open_eyes_glasses = eye_detector.Both_Eyes_Open(gray)
            if len(open_eyes_glasses) == 2:
                #Trying to Detect Both Eyes
                eyes_detected[name]+='1'
                for (ex,ey,ew,eh) in open_eyes_glasses:
                    cv2.rectangle(face,(ex,ey),(ex+ew,ey+eh),(0,255,0),2)        
            else:
                #Trying Individual Eye Detection
                left_eye,right_eye,left_face,right_face = eye_detector.Initilize_EyeHaarcascade(frame,gray,x,y,w,h)
                self.eye_status = eye_detector.Right_Eye_Open_Detect(right_eye,right_face)
                self.eye_status = eye_detector.Left_Eye_Open_Detect(left_eye,left_face)
                eyes_detected[name] += self.eye_status

            if eye_detector.isBlinking(eyes_detected[name],3):
                self.display_name(frame,name,x,y,w,h)
            else:
                if(timer_check):
                    text = "[LOG] Identifying"
                else:
                    text = "[LOG] Possibly Spoofing"
                self.display_name(frame,text,x,y,w,h)
        return frame


def timer():
   now = time.localtime(time.time())
   return now[5]
   

if __name__ == "__main__":
    initializer = Initializer()
    detector = Detector()

    (model, face_detector, open_eyes_detector,left_eye_detector,right_eye_detector, video_capture, images) = initializer.getInitialized_data()
    data = initializer.process_and_encode(images)

    eyes_detected = defaultdict(str)
    initial_check = True
    timer_check = True
    while True:
        if(initial_check and timer_check):
            currenttimer = timer()
            if(currenttimer<40):
                waiting_time_final = currenttimer+18
            else:
                waiting_time_final = 10
            initial_check = False
        elif(timer()>waiting_time_final):
            timer_check = False
            print("[LOG] Identification End")

        if timer_check:
            print("[LOG] Timer: ",timer(), " , ",waiting_time_final)

        frame = detector.detect_and_display(model, video_capture, face_detector, open_eyes_detector,left_eye_detector,right_eye_detector, data, eyes_detected,timer_check)
        cv2.imshow("[LOG] Student Attendence Window", frame)
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break
    cv2.destroyAllWindows()
    video_capture.stop()