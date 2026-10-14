import pathlib, subprocess, sys, tempfile
source = pathlib.Path(sys.argv[1]).read_text()
start = source.index('void EventStringifier::defaultHandler(')
end = source.index('void EventStringifier::handleCtcpAction(', start)
method = source[start:end]
harness = r'''
#include <QString>
#include <vector>
#include <cassert>
namespace EventManager { enum Flag { Self }; }
namespace Message { enum Type { Plain, Server }; }
struct CtcpEvent {
    enum Type { Query, Reply };
    Type type = Query;
    QString cmd = "DCC", parameters = "CHAT chat 1557997490 5060", response;
    bool self = false;
    Type ctcpType() const { return type; }
    QString ctcpCmd() const { return cmd; }
    QString param() const { return parameters; }
    QString reply() const { return response; }
    QString prefix() const { return "openmud!~openmud@host"; }
    QString target() const { return "player"; }
    bool testFlag(EventManager::Flag) const { return self; }
};
QString nickFromMask(QString mask) { return mask.section('!', 0, 0); }
struct EventStringifier {
    struct Output { Message::Type type; QString text, sender, target; };
    std::vector<Output> output;
    QString tr(const char *text) { return QString::fromUtf8(text); }
    void displayMsg(CtcpEvent*, Message::Type type, QString text, QString sender={}, QString target={}) {
        output.push_back({type,text,sender,target});
    }
    void defaultHandler(const QString&, CtcpEvent*);
};
'''
harness += method
harness += r'''
int main() {
    CtcpEvent event;
    EventStringifier stringifier;
    auto run = [&]() { stringifier.output.clear(); stringifier.defaultHandler(event.cmd, &event); };
    run();
    assert(stringifier.output.size() == 1);
    auto result = stringifier.output[0];
    assert(result.type == Message::Plain);
    assert(result.text == "DCC CHAT chat 1557997490 5060");
    assert(result.sender == "openmud!~openmud@host");
    assert(result.target == "player");
    event.parameters = "chat chat 2001:db8::1 5060";
    run(); assert(stringifier.output[0].type == Message::Plain);
    event.parameters = "SEND file 1557997490 5060 100";
    run(); assert(stringifier.output[0].type == Message::Server);
    event.cmd = "VERSION";
    run(); assert(stringifier.output[0].type == Message::Server);
    event.cmd = "DCC"; event.parameters = "CHAT chat 1557997490 5060";
    event.self = true;
    run(); assert(stringifier.output[0].type == Message::Server);
    event.self = false; event.type = CtcpEvent::Reply;
    run(); assert(stringifier.output[0].type == Message::Server);
    event.self = true;
    run(); assert(stringifier.output.empty());
}
'''
with tempfile.TemporaryDirectory(prefix='quassel-dcc-regression-') as directory:
    cpp = pathlib.Path(directory)/'test.cpp'
    binary = pathlib.Path(directory)/'test'
    cpp.write_text(harness)
    qt_flags = subprocess.check_output(['pkg-config','--cflags','--libs','Qt5Core'],text=True).split()
    subprocess.run(['g++','-std=c++14','-fPIC',str(cpp),'-o',str(binary),*qt_flags],check=True)
    subprocess.run([str(binary)],check=True)
print('PASS: actual patched CTCP handler preserves CHAT endpoint, sender and target; SEND, VERSION, replies and self events retain existing behavior.')
