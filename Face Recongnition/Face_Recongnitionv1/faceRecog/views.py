from django.shortcuts import render, redirect
import cv2
import numpy as np
import logging
from sklearn.model_selection import train_test_split
from . import dataset_fetch as df
from . import cascade as casc
from PIL import Image

from time import time
from sklearn.decomposition import PCA
from sklearn.model_selection import GridSearchCV
from sklearn.svm import SVC
from sklearn.metrics import classification_report
from sklearn.metrics import confusion_matrix
import matplotlib.pyplot as plt
import pickle

from settings import BASE_DIR

def index(request):
    return render(request, 'index.html')
def errorImg(request):
    return render(request, 'error.html')

def create_dataset(request):
    userId = request.POST['userId']

    faceDetect = cv2.CascadeClassifier(BASE_DIR+'/ml/haarcascade_frontalface_default.xml')
    cam = cv2.VideoCapture(0)

    id = userId
    sampleNum = 0
 
    while(True):
        ret, img = cam.read()
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        faces = faceDetect.detectMultiScale(gray, 1.3, 5)
        
        for(x,y,w,h) in faces:
            sampleNum = sampleNum+1
            cv2.imwrite(BASE_DIR+'/ml/dataset/user.'+str(id)+'.'+str(sampleNum)+'.jpg', gray[y:y+h,x:x+w])
            cv2.rectangle(img,(x,y),(x+w,y+h), (0,255,0), 2)
            cv2.waitKey(250)

        cv2.imshow("Face",img)
        cv2.waitKey(1)
        if(sampleNum>35):
            break

    cam.release()
    cv2.destroyAllWindows()

    return redirect('/')

def trainer(request):
    import os
    from PIL import Image

    recognizer = cv2.face.LBPHFaceRecognizer_create()

    path = BASE_DIR+'/ml/dataset'


    def getImagesWithID(path):
        imagePaths = [os.path.join(path,f) for f in os.listdir(path)]
        
        faces = []
        Ids = []
        for imagePath in imagePaths:
            faceImg = Image.open(imagePath).convert('L')
            faceNp = np.array(faceImg, 'uint8')
            ID = int(os.path.split(imagePath)[-1].split('.')[1]) 
            faces.append(faceNp)
            Ids.append(ID)
            cv2.imshow("training", faceNp)
            cv2.waitKey(10)
        return np.array(Ids), np.array(faces)

    ids, faces = getImagesWithID(path)
    recognizer.train(faces, ids)

    recognizer.save(BASE_DIR+'/ml/recognizer/trainingData.yml')
    cv2.destroyAllWindows()

    return redirect('/')


def detect(request):
    faceDetect = cv2.CascadeClassifier(BASE_DIR+'/ml/haarcascade_frontalface_default.xml')

    cam = cv2.VideoCapture(0)
    rec = cv2.face.LBPHFaceRecognizer_create();

    rec.read(BASE_DIR+'/ml/recognizer/trainingData.yml')
    getId = 0
    font = cv2.FONT_HERSHEY_SIMPLEX
    userId = 0
    while(True):
        ret, img = cam.read()
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        faces = faceDetect.detectMultiScale(gray, 1.3, 5)
        for(x,y,w,h) in faces:
            cv2.rectangle(img,(x,y),(x+w,y+h), (0,255,0), 2)

            getId,conf = rec.predict(gray[y:y+h, x:x+w]) 

            #print conf;
            if conf<35:
                userId = getId
                cv2.putText(img, "Detected",(x,y+h), font, 2, (0,255,0),2)
            else:
                cv2.putText(img, "Unknown",(x,y+h), font, 2, (0,0,255),2)


        cv2.imshow("Face",img)
        if(cv2.waitKey(1) == ord('q')):
            break
        elif(userId != 0):
            cv2.waitKey(1000)
            cam.release()
            cv2.destroyAllWindows()
            return redirect('/records/details/'+str(userId))

    cam.release()
    cv2.destroyAllWindows()
    return redirect('/')

def eigenTrain(request):
    path = BASE_DIR+'/ml/dataset'

    ids, faces, h, w= df.getImagesWithID(path)
    print 'features'+str(faces.shape[1])
    X_train, X_test, y_train, y_test = train_test_split(faces, ids, test_size=0.25, random_state=42)

    n_classes = y_test.size
    target_names = ['Syed Faizan', 'Iqra','Zubair']
    n_components = 15
    print("Extracting the top %d eigenfaces from %d faces"
          % (n_components, X_train.shape[0]))
    t0 = time()

    pca = PCA(n_components=n_components, svd_solver='randomized',whiten=True).fit(X_train)

    print("done in %0.3fs" % (time() - t0))
    eigenfaces = pca.components_.reshape((n_components, h, w))
    print("Projecting the input data on the eigenfaces orthonormal basis")
    t0 = time()
    X_train_pca = pca.transform(X_train)
    X_test_pca = pca.transform(X_test)
    print("done in %0.3fs" % (time() - t0))

    print("Fitting the classifier to the training set")
    t0 = time()
    param_grid = {'C': [1e3, 5e3, 1e4, 5e4, 1e5],
                  'gamma': [0.0001, 0.0005, 0.001, 0.005, 0.01, 0.1], }
    clf = GridSearchCV(SVC(kernel='rbf', class_weight='balanced'), param_grid)
    clf = clf.fit(X_train_pca, y_train)
    print("done in %0.3fs" % (time() - t0))
    print("Best estimator found by grid search:")
    print(clf.best_estimator_)

    print("Predicting people's names on the test set")
    t0 = time()
    y_pred = clf.predict(X_test_pca)
    print("Predicted labels: ",y_pred)
    print("done in %0.3fs" % (time() - t0))

    print(classification_report(y_test, y_pred, target_names=target_names))

    def plot_gallery(images, titles, h, w, n_row=3, n_col=4):
        """Helper function to plot a gallery of portraits"""
        plt.figure(figsize=(1.8 * n_col, 2.4 * n_row))
        plt.subplots_adjust(bottom=0, left=.01, right=.99, top=.90, hspace=.35)
        for i in range(n_row * n_col):
            plt.subplot(n_row, n_col, i + 1)
            plt.imshow(images[i].reshape((h, w)), cmap=plt.cm.gray)
            plt.title(titles[i], size=12)
            plt.xticks(())
            plt.yticks(())


    eigenface_titles = ["eigenface %d" % i for i in range(eigenfaces.shape[0])]
    plot_gallery(eigenfaces, eigenface_titles, h, w)


    '''
        -- Saving classifier state with pickle
    '''
    svm_pkl_filename = BASE_DIR+'/ml/serializer/svm_classifier.pkl'

    svm_model_pkl = open(svm_pkl_filename, 'wb')
    pickle.dump(clf, svm_model_pkl)

    svm_model_pkl.close()



    pca_pkl_filename = BASE_DIR+'/ml/serializer/pca_state.pkl'
    pca_pkl = open(pca_pkl_filename, 'wb')
    pickle.dump(pca, pca_pkl)
    pca_pkl.close()

    plt.show()

    return redirect('/')


def detectImage(request):
    userImage = request.FILES['userImage']

    svm_pkl_filename =  BASE_DIR+'/ml/serializer/svm_classifier.pkl'

    svm_model_pkl = open(svm_pkl_filename, 'rb')
    svm_model = pickle.load(svm_model_pkl)

    pca_pkl_filename =  BASE_DIR+'/ml/serializer/pca_state.pkl'

    pca_model_pkl = open(pca_pkl_filename, 'rb')
    pca = pickle.load(pca_model_pkl)

    '''
    First Save image as cv2.imread only accepts path
    '''
    im = Image.open(userImage)
    imgPath = BASE_DIR+'/ml/uploadedImages/'+str(userImage)
    im.save(imgPath, 'JPEG')

    '''
    Input Image
    '''
    try:
        inputImg = casc.facecrop(imgPath)
        inputImg.show()
    except :
        print("No face detected, or image not recognized")
        return redirect('/error_image')

    imgNp = np.array(inputImg, 'uint8')
    imgFlatten = imgNp.flatten()

    imgArrTwoD = []
    imgArrTwoD.append(imgFlatten)

    img_pca = pca.transform(imgArrTwoD)


    pred = svm_model.predict(img_pca)
    print(svm_model.best_estimator_)
    print pred[0]

    return redirect('/records/details/'+str(pred[0]))
