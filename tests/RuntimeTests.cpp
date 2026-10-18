#include "visual_runtime_state.h"
#include "http_server.h"
#include "track_store.h"
#include "websocket_server.h"
#include <QCoreApplication>
#include <QJsonDocument>
#include <QJsonObject>
#include <QNetworkAccessManager>
#include <QNetworkRequest>
#include <QNetworkReply>
#include <QEventLoop>
#include <QTimer>
#include <QTcpSocket>
#include <QThread>
#include <iostream>
#include <stdexcept>

void Check(bool ok, const char* text) { if (!ok) throw std::runtime_error(text); }
QJsonObject Event(QString id, int priority = 20, int minimum = 0, QString key = "", int ttl = 8000) {
 return {{"type","system.toast.v1"},{"version",1},{"eventId",id},{"priority",priority},{"ttlMs",ttl},
  {"payload",QJsonObject{{"message",id}}},{"queue",QJsonObject{{"minDisplayMs",minimum},{"replaceByKey",key},{"interruptible",true}}}};
}
QString Active(VisualRuntimeState& state) { return state.Snapshot()["activeEvent"].toObject()["eventId"].toString(); }
void QueueTests() {
 VisualRuntimeState state; QString err;
 Check(state.EnqueueEnvelope(Event("low",10,50,"loading"),&err),"enqueue");
 state.EnqueueEnvelope(Event("high",90),&err); Check(Active(state)=="low","minimum display ignored");
 QThread::msleep(70); Check(Active(state)=="high","high priority not promoted");
 state.Clear();state.EnqueueEnvelope(Event("old",20,50,"same"),&err);state.EnqueueEnvelope(Event("new",20,0,"same"),&err);
 Check(Active(state)=="old","replacement ignored minimum");QThread::msleep(70);Check(Active(state)=="new","replacement stuck");
 auto hide=Event("hide");hide["type"]="overlay.hide.v1";hide["payload"]=QJsonObject{{"replaceKey","same"}};
 state.EnqueueEnvelope(hide,&err);Check(Active(state).isEmpty(),"hide rendered as card");
 state.EnqueueEnvelope(Event("long",90),&err);state.EnqueueEnvelope(Event("stale",10,0,"",1000),&err);
 QThread::msleep(1050);Check(state.Snapshot()["pendingCount"].toInt()==0,"expired pending survived");
 state.Clear();auto blocking=Event("block",90);blocking["queue"]=QJsonObject{{"interruptible",false},{"replaceByKey","block"}};
 state.EnqueueEnvelope(blocking,&err);state.EnqueueEnvelope(Event("first",20,0,"first"),&err);state.EnqueueEnvelope(Event("second",20,0,"second"),&err);
 hide["payload"]=QJsonObject{{"replaceKey","block"}};state.EnqueueEnvelope(hide,&err);Check(Active(state)=="first","equal priority FIFO");
 hide["payload"]=QJsonObject{{"replaceKey","first"}};state.EnqueueEnvelope(hide,&err);Check(Active(state)=="second","second FIFO");
 hide["payload"]=QJsonObject{{"replaceKey",""}};Check(!state.EnqueueEnvelope(hide,&err),"invalid hide accepted");
 std::cout<<"Queue tests passed\n";
}
QByteArray Request(QString path, QByteArray body = {}, int expected = 200) {
 QNetworkAccessManager manager;QNetworkRequest request(QUrl("http://127.0.0.1:17408"+path));
 request.setHeader(QNetworkRequest::ContentTypeHeader,"application/json");
 auto reply=body.isEmpty()?manager.get(request):manager.post(request,body);
 QEventLoop loop;QTimer timeout;timeout.setSingleShot(true);QObject::connect(reply,&QNetworkReply::finished,&loop,&QEventLoop::quit);QObject::connect(&timeout,&QTimer::timeout,&loop,&QEventLoop::quit);timeout.start(3000);loop.exec();
 Check(reply->isFinished(),"HTTP timeout");Check(reply->attribute(QNetworkRequest::HttpStatusCodeAttribute).toInt()==expected,"HTTP status");
 return reply->readAll();
}
int main(int argc,char**argv) {
 QCoreApplication app(argc,argv);
 try {
  QString data=argc>1?QString::fromLocal8Bit(argv[1]):"";bool serve=argc>2&&QString(argv[2])=="--serve";
  if(!serve)QueueTests();TrackStore store;VisualRuntimeState runtime;HttpServer http(&store,&runtime,data);QString error;
  Check(http.Start(17408,&error),"HTTP start");
  if(serve){std::cout<<"Serving on 17408\n"<<std::flush;return app.exec();}
  Check(Request("/health").contains("enabled"),"health");Check(Request("/admin").contains("Streamer OS"),"admin route");
  Check(Request("/admin.js").contains("settingsJson"),"admin JS");
  Request("/events",QJsonDocument(Event("preview")).toJson());Check(Request("/runtime/state").contains("preview"),"event HTTP");
  Request("/runtime/clear","{}");Check(Request("/runtime/state").contains("\"activeEvent\":null"),"clear HTTP");
  Request("/events","{broken",400);http.Stop();Check(http.Start(17408,&error),"HTTP restart");http.Stop();
  WebSocketServer ws;
  for(int i=0;i<2;i++){
   Check(ws.Start(17407,&error),"WebSocket restart");QTcpSocket socket;socket.connectToHost("127.0.0.1",17407);Check(socket.waitForConnected(2000),"WS TCP");
   socket.write("GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n\r\n");socket.flush();Check(socket.waitForReadyRead(2000),"WS handshake");Check(socket.readAll().contains("101"),"WS upgrade");
   ws.Broadcast(QJsonObject{{"type","fft"}});Check(socket.waitForReadyRead(2000),"WS broadcast");Check(socket.readAll().contains("fft"),"WS payload");socket.abort();ws.Stop();
  }
  std::cout<<"HTTP/WebSocket tests passed\n";return 0;
 }catch(const std::exception& e){std::cerr<<e.what()<<"\n";return 1;}
}
